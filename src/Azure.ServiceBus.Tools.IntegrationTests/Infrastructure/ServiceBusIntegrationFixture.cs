using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Azure.ServiceBus.Tools.IntegrationTests;

public sealed class ServiceBusIntegrationFixture : IAsyncLifetime
{
    private const string MssqlSaPassword = "AsbEmulator_StrongPassw0rd!";
    private const string SharedAccessKey = "SAS_KEY_VALUE";
    private const string SharedAccessKeyName = "RootManageSharedAccessKey";
    private const string EmulatorHttpPort = "5300";

    private readonly List<string> queues = new();
    private readonly List<(string Topic, string Subscription)> subscriptions = new();
    private readonly List<string> topics = new();
    private readonly INetwork network = new NetworkBuilder()
        .WithName($"asb-it-net-{Guid.NewGuid():N}")
        .Build();
    private readonly IContainer sqlContainer;
    private readonly IContainer emulatorContainer;
    private readonly string configPath;

    private ServiceBusClient? serviceBusClient;
    private ServiceBusAdministrationClient? adminClient;
    private string? dataConnectionString;
    private string? managementConnectionString;

    public ServiceBusIntegrationFixture()
    {
        configPath = CreateEmulatorConfigFile();

        sqlContainer = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithName($"asb-it-mssql-{Guid.NewGuid():N}")
            .WithNetwork(network)
            .WithNetworkAliases("mssql")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", MssqlSaPassword)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
            .Build();

        emulatorContainer = new ContainerBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithName($"asb-it-emulator-{Guid.NewGuid():N}")
            .WithNetwork(network)
            .WithPortBinding(5672, true)
            .WithPortBinding(5300, true)
            .WithBindMount(configPath, "/ServiceBus_Emulator/ConfigFiles/Config.json", AccessMode.ReadOnly)
            .WithEnvironment("SQL_SERVER", "mssql")
            .WithEnvironment("MSSQL_SA_PASSWORD", MssqlSaPassword)
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("EMULATOR_HTTP_PORT", EmulatorHttpPort)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await network.CreateAsync();
        await sqlContainer.StartAsync();
        await emulatorContainer.StartAsync();

        var sbPort = emulatorContainer.GetMappedPublicPort(5672);
        var managementPort = emulatorContainer.GetMappedPublicPort(5300);
        await WaitForEmulatorHealthAsync(managementPort, TimeSpan.FromSeconds(90));
        dataConnectionString =
            $"Endpoint=sb://localhost:{sbPort};SharedAccessKeyName={SharedAccessKeyName};SharedAccessKey={SharedAccessKey};UseDevelopmentEmulator=true;";
        managementConnectionString =
            $"Endpoint=sb://localhost:{managementPort};SharedAccessKeyName={SharedAccessKeyName};SharedAccessKey={SharedAccessKey};UseDevelopmentEmulator=true;";

        serviceBusClient = new ServiceBusClient(dataConnectionString);
        adminClient = new ServiceBusAdministrationClient(managementConnectionString);
    }

    public string GetDataConnectionString()
    {
        return dataConnectionString
            ?? throw new InvalidOperationException("Fixture is not initialized.");
    }

    public string GetManagementConnectionString()
    {
        return managementConnectionString
            ?? throw new InvalidOperationException("Fixture is not initialized.");
    }

    public async Task DisposeAsync()
    {
        if (adminClient is not null)
        {
            foreach (var (topic, subscription) in subscriptions)
            {
                if (await adminClient.SubscriptionExistsAsync(topic, subscription))
                {
                    await adminClient.DeleteSubscriptionAsync(topic, subscription);
                }
            }

            foreach (var topic in topics)
            {
                if (await adminClient.TopicExistsAsync(topic))
                {
                    await adminClient.DeleteTopicAsync(topic);
                }
            }

            foreach (var queue in queues)
            {
                if (await adminClient.QueueExistsAsync(queue))
                {
                    await adminClient.DeleteQueueAsync(queue);
                }
            }
        }

        if (serviceBusClient is not null)
        {
            await serviceBusClient.DisposeAsync();
        }

        await emulatorContainer.DisposeAsync();
        await sqlContainer.DisposeAsync();
        await network.DeleteAsync();

        if (File.Exists(configPath))
        {
            File.Delete(configPath);
        }
    }

    public string CreateUniqueName(string prefix)
    {
        var value = $"{prefix.ToLowerInvariant()}-{Guid.NewGuid():N}";
        return value[..Math.Min(44, value.Length)];
    }

    public async Task<string> CreateQueueAsync(string prefix)
    {
        var queueName = CreateUniqueName(prefix);
        await adminClient!.CreateQueueAsync(queueName);
        queues.Add(queueName);
        return queueName;
    }

    public async Task<string> CreateQueueAsync(string prefix, Action<CreateQueueOptions> configure)
    {
        var queueName = CreateUniqueName(prefix);
        var options = new CreateQueueOptions(queueName);
        configure(options);
        await adminClient!.CreateQueueAsync(options);
        queues.Add(queueName);
        return queueName;
    }

    public async Task<(string Topic, string Subscription)> CreateSubscriptionAsync(string topicPrefix, string subscriptionPrefix)
    {
        var topicName = CreateUniqueName(topicPrefix);
        var subscriptionName = CreateUniqueName(subscriptionPrefix);
        await adminClient!.CreateTopicAsync(topicName);
        await adminClient.CreateSubscriptionAsync(topicName, subscriptionName);
        topics.Add(topicName);
        subscriptions.Add((topicName, subscriptionName));
        return (topicName, subscriptionName);
    }

    public async Task<(string Topic, string Subscription)> CreateSubscriptionAsync(
        string topicPrefix,
        string subscriptionPrefix,
        Action<CreateSubscriptionOptions> configure)
    {
        var topicName = CreateUniqueName(topicPrefix);
        var subscriptionName = CreateUniqueName(subscriptionPrefix);
        await adminClient!.CreateTopicAsync(topicName);

        var options = new CreateSubscriptionOptions(topicName, subscriptionName);
        configure(options);
        await adminClient.CreateSubscriptionAsync(options);

        topics.Add(topicName);
        subscriptions.Add((topicName, subscriptionName));
        return (topicName, subscriptionName);
    }

    public async Task SendMessagesAsync(string queueName, int count)
    {
        await using var sender = serviceBusClient!.CreateSender(queueName);
        var batch = Enumerable.Range(1, count)
            .Select(i => new ServiceBusMessage($"msg-{i}") { MessageId = $"{queueName}-{i}" })
            .ToList();

        await sender.SendMessagesAsync(batch);
    }

    public async Task<IReadOnlyList<long>> ScheduleMessagesAsync(
        string queueName,
        int count,
        DateTimeOffset scheduleAtUtc)
    {
        await using var sender = serviceBusClient!.CreateSender(queueName);
        var sequenceNumbers = new List<long>(count);
        for (var i = 1; i <= count; i++)
        {
            var message = new ServiceBusMessage($"scheduled-msg-{i}") { MessageId = $"{queueName}-scheduled-{i}" };
            var sequenceNumber = await sender.ScheduleMessageAsync(message, scheduleAtUtc);
            sequenceNumbers.Add(sequenceNumber);
        }

        return sequenceNumbers;
    }

    public async Task DeadLetterOneMessageAsync(string queueName)
    {
        await using var receiver = serviceBusClient!.CreateReceiver(queueName);
        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(message);
        await receiver.DeadLetterMessageAsync(message!);

        await using var deadLetterReceiver = serviceBusClient.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < TimeSpan.FromSeconds(10))
        {
            var deadLetterMessage = await deadLetterReceiver.PeekMessageAsync();
            if (deadLetterMessage is not null)
            {
                return;
            }

            await Task.Delay(250);
        }

        Assert.Fail($"Expected a dead-letter message in queue '{queueName}', but none was available within timeout.");
    }

    public async Task<long> GetActiveCountAsync(string queueName)
    {
        var props = await adminClient!.GetQueueRuntimePropertiesAsync(queueName);
        return props.Value.ActiveMessageCount;
    }

    public async Task<long> GetDeadLetterCountAsync(string queueName)
    {
        var props = await adminClient!.GetQueueRuntimePropertiesAsync(queueName);
        return props.Value.DeadLetterMessageCount;
    }

    public async Task WaitForQueueCountAsync(
        string queueName,
        Func<QueueRuntimeProperties, long> selector,
        long expected,
        TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            var runtime = await adminClient!.GetQueueRuntimePropertiesAsync(queueName);
            if (selector(runtime.Value) == expected)
            {
                return;
            }

            await Task.Delay(300);
        }

        var current = await adminClient!.GetQueueRuntimePropertiesAsync(queueName);
        Assert.Fail($"Timed out waiting for queue '{queueName}' count to be {expected}. Current: {selector(current.Value)}");
    }

    public async Task<int> DrainQueueAsync(
        string queueName,
        SourceSubQueue subQueue = SourceSubQueue.None,
        TimeSpan? timeout = null)
    {
        var elapsed = TimeSpan.Zero;
        var max = timeout ?? TimeSpan.FromSeconds(10);
        var total = 0;

        var receiverOptions = new ServiceBusReceiverOptions
        {
            SubQueue = subQueue switch
            {
                SourceSubQueue.None => SubQueue.None,
                SourceSubQueue.DeadLetter => SubQueue.DeadLetter,
                SourceSubQueue.TransferDeadLetter => SubQueue.TransferDeadLetter,
                _ => SubQueue.None
            }
        };

        await using var receiver = serviceBusClient!.CreateReceiver(queueName, receiverOptions);

        while (elapsed < max)
        {
            var messages = await receiver.ReceiveMessagesAsync(maxMessages: 50, TimeSpan.FromSeconds(1));
            if (messages.Count == 0)
            {
                elapsed += TimeSpan.FromSeconds(1);
                continue;
            }

            foreach (var message in messages)
            {
                await receiver.CompleteMessageAsync(message);
                total++;
            }
        }

        return total;
    }

    private static string CreateEmulatorConfigFile()
    {
        var configContent = """
            {
              "UserConfig": {
                "Namespaces": [
                  {
                    "Name": "sbemulatorns",
                    "Queues": [],
                    "Topics": []
                  }
                ],
                "Logging": {
                  "Type": "File"
                }
              }
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"asb-emulator-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, configContent);
        return path;
    }

    private static async Task WaitForEmulatorHealthAsync(int managementPort, TimeSpan timeout)
    {
        using var http = new HttpClient();
        var healthUri = new Uri($"http://localhost:{managementPort}/health");
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                using var response = await http.GetAsync(healthUri);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Health endpoint may not be available yet.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Service Bus emulator health endpoint was not ready within {timeout.TotalSeconds:N0}s.");
    }
}
