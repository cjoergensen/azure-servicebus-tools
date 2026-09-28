using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;
using FluentResults;

namespace Cjoergensen.Azure.ServiceBus.Tools.MessageMover;

public class ServiceBusMessageMover(
    string? connectionString,
    string? serviceBusNamespace,
    SourceEndpoint source,
    TargetEndpoint target,
    MessageMoverBehavior behavior,
    int maxMessagesPerBatch,
    int prefetchCount,
    string identifier = "",
    Func<bool>? canProcessNextMessage = null) : IAsyncDisposable
{
    private readonly ServiceBusClient client = ServiceBusClientFactory.CreateClient(connectionString, serviceBusNamespace);
    private readonly SourceEndpoint source = source;
    private readonly TargetEndpoint target = target;
    private readonly MessageMoverBehavior behavior = behavior;
    private readonly int maxMessagesPerBatch = maxMessagesPerBatch;
    private readonly int prefetchCount = prefetchCount;
    private readonly string identifier = identifier;
    private readonly Func<bool>? canProcessNextMessage = canProcessNextMessage;

    public async IAsyncEnumerable<FluentResults.Result> MoveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var sender = CreateSender();
        await using var receiver = CreateReceiver();
        var consecutiveEmptyReceives = 0;
        var maxConsecutiveEmptyReceives = behavior.MaxMessages.HasValue
            ? SharedConstants.ConsecutiveEmptyReceivesBeforeStopWhenLimited
            : SharedConstants.ConsecutiveEmptyReceivesBeforeStop;

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ServiceBusReceivedMessage> messages = await receiver.ReceiveMessagesAsync(
                maxMessagesPerBatch,
                TimeSpan.FromSeconds(SharedConstants.DefaultReceiveWaitSeconds),
                cancellationToken);

            if (messages.Count == 0)
            {
                consecutiveEmptyReceives++;
                if (consecutiveEmptyReceives >= maxConsecutiveEmptyReceives)
                {
                    yield break;
                }

                await Task.Delay(SharedConstants.EmptyReceiveBackoffMilliseconds, cancellationToken);
                continue;
            }

            consecutiveEmptyReceives = 0;
            foreach (var msg in messages)
            {
                if (canProcessNextMessage is not null && !canProcessNextMessage())
                {
                    yield break;
                }

                yield return await MoveSingleMessageAsync(sender, receiver, msg, cancellationToken);
            }
        }
    }

    private ServiceBusSender CreateSender()
    {
        return client.CreateSender(
            target.EntityName,
            new ServiceBusSenderOptions { Identifier = identifier });
    }

    private ServiceBusReceiver CreateReceiver()
    {
        return ServiceBusReceiverFactory.CreateReceiver(client, source, prefetchCount, identifier);
    }

    private async Task<Result> MoveSingleMessageAsync(
        ServiceBusSender sender,
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!behavior.DryRun)
            {
                await sender.SendMessageAsync(MessageCloner.Clone(message), cancellationToken);
            }

            await ApplySettlementAsync(receiver, message, behavior.OnSuccess, cancellationToken);
            return Result.Ok();
        }
        catch (ServiceBusException ex)
        {
            await ApplySettlementAsync(receiver, message, behavior.OnFailure, cancellationToken);
            return Result.Fail($"Failed to move message '{message.MessageId}': {ex.Reason} - {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            await ApplySettlementAsync(receiver, message, behavior.OnFailure, cancellationToken);
            return Result.Fail($"Failed to move message '{message.MessageId}': {ex.Message}");
        }
    }

    private static async Task ApplySettlementAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        SettlementMode mode,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case SettlementMode.Complete:
                await receiver.CompleteMessageAsync(message, cancellationToken);
                break;
            case SettlementMode.Abandon:
                await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                break;
            case SettlementMode.DeadLetter:
                await receiver.DeadLetterMessageAsync(message, cancellationToken: cancellationToken);
                break;
            case SettlementMode.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported settlement mode.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
    }
}