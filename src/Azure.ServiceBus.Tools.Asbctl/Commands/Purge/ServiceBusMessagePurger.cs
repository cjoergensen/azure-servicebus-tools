using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;
using FluentResults;

namespace Cjoergensen.Azure.ServiceBus.Tools.Purger;

public class ServiceBusMessagePurger(
    string? connectionString,
    string? serviceBusNamespace,
    SourceEndpoint source,
    int maxMessagesPerBatch,
    int prefetchCount,
    string identifier = "") : IAsyncDisposable
{
    private readonly ServiceBusClient client = ServiceBusClientFactory.CreateClient(connectionString, serviceBusNamespace);
    private readonly SourceEndpoint source = source;
    private readonly int maxMessagesPerBatch = maxMessagesPerBatch;
    private readonly int prefetchCount = prefetchCount;
    private readonly string identifier = identifier;

    public async IAsyncEnumerable<Result> PurgeMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var receiver = CreateReceiver();

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ServiceBusReceivedMessage> messages = await receiver.ReceiveMessagesAsync(
                maxMessagesPerBatch,
                TimeSpan.FromSeconds(SharedConstants.DefaultReceiveWaitSeconds),
                cancellationToken);

            if (messages.Count == 0)
            {
                yield break;
            }

            foreach (var msg in messages)
            {
                yield return await PurgeSingleMessageAsync(receiver, msg, cancellationToken);
            }
        }
    }

    private static async Task<Result> PurgeSingleMessageAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await receiver.CompleteMessageAsync(message, cancellationToken);
            return Result.Ok();
        }
        catch (ServiceBusException ex)
        {
            return Result.Fail($"Failed to purge message '{message.MessageId}': {ex.Reason} - {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail($"Failed to purge message '{message.MessageId}': {ex.Message}");
        }
    }

    private ServiceBusReceiver CreateReceiver()
    {
        return ServiceBusReceiverFactory.CreateReceiver(client, source, prefetchCount, identifier);
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }
}
