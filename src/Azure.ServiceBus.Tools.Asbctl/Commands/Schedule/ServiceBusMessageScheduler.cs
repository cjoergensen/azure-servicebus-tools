using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Schedule;

public class ServiceBusMessageScheduler(
    string? connectionString,
    string? serviceBusNamespace,
    SourceEndpoint source,
    ScheduleBehavior behavior,
    int maxMessagesPerBatch,
    int prefetchCount,
    string identifier = "",
    long? initialSourceMessageCount = null) : IAsyncDisposable
{
    private readonly ServiceBusClient client = ServiceBusClientFactory.CreateClient(connectionString, serviceBusNamespace);
    private readonly SourceEndpoint source = source;
    private readonly ScheduleBehavior behavior = behavior;
    private readonly int maxMessagesPerBatch = maxMessagesPerBatch;
    private readonly int prefetchCount = prefetchCount;
    private readonly string identifier = identifier;
    private readonly long? initialSourceMessageCount = initialSourceMessageCount;

    public async IAsyncEnumerable<ScheduleOperationResult> ScheduleMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var receiver = CreateReceiver();
        await using var sender = client.CreateSender(source.EntityName);

        if (behavior.ScheduleAll)
        {
            await foreach (var result in ScheduleAllMessagesAsync(receiver, sender, cancellationToken))
            {
                yield return result;
            }

            yield break;
        }

        await foreach (var result in ScheduleSingleMessageAsync(receiver, sender, cancellationToken))
        {
            yield return result;
        }
    }

    private async IAsyncEnumerable<ScheduleOperationResult> ScheduleAllMessagesAsync(
        ServiceBusReceiver receiver,
        ServiceBusSender sender,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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

            foreach (var message in messages)
            {
                yield return await ScheduleSingleReceivedMessageAsync(receiver, sender, message, cancellationToken);
            }
        }
    }

    private async IAsyncEnumerable<ScheduleOperationResult> ScheduleSingleMessageAsync(
        ServiceBusReceiver receiver,
        ServiceBusSender sender,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var targetMessageId = behavior.MessageId!;
        var maxScans = initialSourceMessageCount.HasValue
            ? Math.Max(1, initialSourceMessageCount.Value)
            : long.MaxValue;
        long scanned = 0;

        while (!cancellationToken.IsCancellationRequested && scanned < maxScans)
        {
            IReadOnlyList<ServiceBusReceivedMessage> messages = await receiver.ReceiveMessagesAsync(
                maxMessagesPerBatch,
                TimeSpan.FromSeconds(SharedConstants.DefaultReceiveWaitSeconds),
                cancellationToken);

            if (messages.Count == 0)
            {
                break;
            }

            foreach (var message in messages)
            {
                scanned++;
                if (string.Equals(message.MessageId, targetMessageId, StringComparison.Ordinal))
                {
                    yield return await ScheduleSingleReceivedMessageAsync(receiver, sender, message, cancellationToken);
                    yield break;
                }

                await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                if (scanned >= maxScans)
                {
                    break;
                }
            }
        }

        yield return new ScheduleOperationResult(
            false,
            targetMessageId,
            $"Message with id '{targetMessageId}' was not found in source '{source.DisplayName}'.");
    }

    private async Task<ScheduleOperationResult> ScheduleSingleReceivedMessageAsync(
        ServiceBusReceiver receiver,
        ServiceBusSender sender,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var clonedMessage = CloneForScheduledMessage(message, behavior.ScheduleAtUtc);
            await sender.ScheduleMessageAsync(clonedMessage, behavior.ScheduleAtUtc, cancellationToken);
            await receiver.CompleteMessageAsync(message, cancellationToken);
            return new ScheduleOperationResult(true, message.MessageId);
        }
        catch (ServiceBusException ex)
        {
            return new ScheduleOperationResult(
                false,
                message.MessageId,
                $"Failed to schedule message '{message.MessageId}': {ex.Reason} - {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return new ScheduleOperationResult(
                false,
                message.MessageId,
                $"Failed to schedule message '{message.MessageId}': {ex.Message}");
        }
    }

    private ServiceBusReceiver CreateReceiver()
    {
        return ServiceBusReceiverFactory.CreateReceiver(client, source, prefetchCount, identifier);
    }

    private static ServiceBusMessage CloneForScheduledMessage(
        ServiceBusReceivedMessage sourceMessage,
        DateTimeOffset scheduleAtUtc)
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
            To = sourceMessage.To,
            ScheduledEnqueueTime = scheduleAtUtc
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
