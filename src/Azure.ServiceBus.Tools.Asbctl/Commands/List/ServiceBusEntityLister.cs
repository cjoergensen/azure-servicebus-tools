using Azure;
using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityLister;

public sealed class ServiceBusEntityLister(string? connectionString, string? serviceBusNamespace)
{
    private readonly ServiceBusAdministrationClient adminClient =
        ServiceBusClientFactory.CreateAdministrationClient(connectionString, serviceBusNamespace);

    public async Task<ListResult> ListQueuesAsync(CancellationToken cancellationToken = default)
    {
        var propertiesTask = CollectAsync(adminClient.GetQueuesAsync(cancellationToken), q => q.Name);
        var runtimeTask = CollectAsync(adminClient.GetQueuesRuntimePropertiesAsync(cancellationToken), r => r.Name);

        await Task.WhenAll(propertiesTask, runtimeTask);

        var propertiesMap = propertiesTask.Result;
        var runtimeMap = runtimeTask.Result;

        var entries = propertiesMap.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var props = propertiesMap[name];
                var runtime = runtimeMap.TryGetValue(name, out var r) ? r : null;
                return new QueueListEntry(
                    name,
                    props.Status,
                    runtime?.ActiveMessageCount ?? 0,
                    runtime?.DeadLetterMessageCount ?? 0,
                    runtime?.ScheduledMessageCount ?? 0,
                    runtime?.TotalMessageCount ?? 0,
                    runtime?.SizeInBytes ?? 0,
                    props.MaxSizeInMegabytes);
            })
            .ToList();

        return ListResult.ForQueues(entries);
    }

    public async Task<ListResult> ListTopicsAsync(CancellationToken cancellationToken = default)
    {
        var propertiesTask = CollectAsync(adminClient.GetTopicsAsync(cancellationToken), t => t.Name);
        var runtimeTask = CollectAsync(adminClient.GetTopicsRuntimePropertiesAsync(cancellationToken), r => r.Name);

        await Task.WhenAll(propertiesTask, runtimeTask);

        var propertiesMap = propertiesTask.Result;
        var runtimeMap = runtimeTask.Result;

        var entries = propertiesMap.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var props = propertiesMap[name];
                var runtime = runtimeMap.TryGetValue(name, out var r) ? r : null;
                return new TopicListEntry(
                    name,
                    props.Status,
                    runtime?.SubscriptionCount ?? 0,
                    runtime?.ScheduledMessageCount ?? 0,
                    runtime?.SizeInBytes ?? 0,
                    props.MaxSizeInMegabytes);
            })
            .ToList();

        return ListResult.ForTopics(entries);
    }

    public async Task<ListResult> ListSubscriptionsAsync(string topicName, CancellationToken cancellationToken = default)
    {
        var propertiesTask = CollectAsync(adminClient.GetSubscriptionsAsync(topicName, cancellationToken), s => s.SubscriptionName);
        var runtimeTask = CollectAsync(adminClient.GetSubscriptionsRuntimePropertiesAsync(topicName, cancellationToken), r => r.SubscriptionName);

        await Task.WhenAll(propertiesTask, runtimeTask);

        var propertiesMap = propertiesTask.Result;
        var runtimeMap = runtimeTask.Result;

        var entries = propertiesMap.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var props = propertiesMap[name];
                var runtime = runtimeMap.TryGetValue(name, out var r) ? r : null;
                return new SubscriptionListEntry(
                    name,
                    props.Status,
                    runtime?.ActiveMessageCount ?? 0,
                    runtime?.DeadLetterMessageCount ?? 0,
                    runtime?.TransferDeadLetterMessageCount ?? 0,
                    runtime?.TotalMessageCount ?? 0,
                    props.MaxDeliveryCount,
                    props.LockDuration);
            })
            .ToList();

        return ListResult.ForSubscriptions(topicName, entries);
    }

    private static async Task<Dictionary<string, T>> CollectAsync<T>(
        AsyncPageable<T> pageable,
        Func<T, string> keySelector)
        where T : notnull
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        await foreach (var item in pageable)
        {
            result[keySelector(item)] = item;
        }
        return result;
    }
}
