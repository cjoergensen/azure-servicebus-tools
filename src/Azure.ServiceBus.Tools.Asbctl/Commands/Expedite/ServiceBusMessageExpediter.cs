using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Expedite;

public class ServiceBusMessageExpediter(
    string? connectionString,
    string? serviceBusNamespace,
    SourceEndpoint source,
    ExpediteBehavior behavior,
    int peekBatchSize,
    int prefetchCount,
    string identifier = "") : IAsyncDisposable
{
    private readonly ServiceBusClient client = ServiceBusClientFactory.CreateClient(connectionString, serviceBusNamespace);
    private readonly SourceEndpoint source = source;
    private readonly ExpediteBehavior behavior = behavior;
    private readonly int peekBatchSize = peekBatchSize;
    private readonly int prefetchCount = prefetchCount;
    private readonly string identifier = identifier;

    public async IAsyncEnumerable<ExpediteOperationResult> ExpediteMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var receiver = CreateReceiver();
        await using var sender = client.CreateSender(source.EntityName);

        if (behavior.ExpediteAll)
        {
            await foreach (var result in ExpediteAllAsync(receiver, sender, cancellationToken))
            {
                yield return result;
            }

            yield break;
        }

        var found = await TryPeekScheduledMessageByIdAsync(
            receiver,
            behavior.MessageId!,
            cancellationToken);

        if (found is null)
        {
            yield return new ExpediteOperationResult(
                false,
                behavior.MessageId!,
                $"Scheduled message with id '{behavior.MessageId}' was not found in queue '{source.EntityName}'.");
            yield break;
        }

        yield return await ExpediteSingleScheduledMessageAsync(sender, found, cancellationToken);
    }

    private async IAsyncEnumerable<ExpediteOperationResult> ExpediteAllAsync(
        ServiceBusReceiver receiver,
        ServiceBusSender sender,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long? fromSequenceNumber = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ServiceBusReceivedMessage> peeked = await receiver.PeekMessagesAsync(
                peekBatchSize,
                fromSequenceNumber,
                cancellationToken);

            if (peeked.Count == 0)
            {
                yield break;
            }

            foreach (var message in peeked)
            {
                fromSequenceNumber = message.SequenceNumber + 1;
                if (message.ScheduledEnqueueTime <= DateTimeOffset.UtcNow)
                {
                    continue;
                }

                yield return await ExpediteSingleScheduledMessageAsync(sender, message, cancellationToken);
            }
        }
    }

    private async Task<ServiceBusReceivedMessage?> TryPeekScheduledMessageByIdAsync(
        ServiceBusReceiver receiver,
        string messageId,
        CancellationToken cancellationToken)
    {
        long? fromSequenceNumber = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ServiceBusReceivedMessage> peeked = await receiver.PeekMessagesAsync(
                peekBatchSize,
                fromSequenceNumber,
                cancellationToken);

            if (peeked.Count == 0)
            {
                return null;
            }

            foreach (var message in peeked)
            {
                fromSequenceNumber = message.SequenceNumber + 1;
                if (!string.Equals(message.MessageId, messageId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (message.ScheduledEnqueueTime <= DateTimeOffset.UtcNow)
                {
                    return null;
                }

                return message;
            }
        }

        return null;
    }

    private async Task<ExpediteOperationResult> ExpediteSingleScheduledMessageAsync(
        ServiceBusSender sender,
        ServiceBusReceivedMessage scheduledMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var immediateMessage = CloneForImmediateSend(scheduledMessage);
            await sender.SendMessageAsync(immediateMessage, cancellationToken);
            await sender.CancelScheduledMessageAsync(scheduledMessage.SequenceNumber, cancellationToken);
            return new ExpediteOperationResult(true, scheduledMessage.MessageId);
        }
        catch (ServiceBusException ex)
        {
            return new ExpediteOperationResult(
                false,
                scheduledMessage.MessageId,
                $"Failed to expedite message '{scheduledMessage.MessageId}': {ex.Reason} - {ex.Message}");
        }
    }

    private ServiceBusReceiver CreateReceiver()
    {
        return ServiceBusReceiverFactory.CreateReceiver(client, source, prefetchCount, identifier);
    }

    private static ServiceBusMessage CloneForImmediateSend(ServiceBusReceivedMessage sourceMessage)
    {
        var clonedMessage = new ServiceBusMessage(sourceMessage.Body)
        {
            ContentType = sourceMessage.ContentType,
            CorrelationId = sourceMessage.CorrelationId,
            Subject = sourceMessage.Subject,
            MessageId = sourceMessage.MessageId,
            TimeToLive = sourceMessage.TimeToLive,
            SessionId = sourceMessage.SessionId,
            PartitionKey = sourceMessage.PartitionKey,
            ReplyTo = sourceMessage.ReplyTo,
            ReplyToSessionId = sourceMessage.ReplyToSessionId,
            To = sourceMessage.To
        };

        foreach (var prop in sourceMessage.ApplicationProperties)
        {
            clonedMessage.ApplicationProperties.Add(prop.Key, prop.Value);
        }

        return clonedMessage;
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }
}
