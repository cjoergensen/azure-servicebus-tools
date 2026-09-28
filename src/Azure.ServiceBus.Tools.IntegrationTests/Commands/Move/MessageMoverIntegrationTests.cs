using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.IntegrationTests;

[Collection(ServiceBusCollection.Name)]
[Trait("Category", "Integration")]
public class MessageMoverIntegrationTests(ServiceBusIntegrationFixture fixture)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task MoveMessagesAsync_QueueToQueue_MovesAllMessages()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-mover-source");
        var targetQueue = await fixture.CreateQueueAsync("it-mover-target");
        await fixture.SendMessagesAsync(sourceQueue, 3);

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.None);
        var target = new TargetEndpoint(targetQueue);
        var behavior = new MessageMoverBehavior(
            SettlementMode.Complete,
            SettlementMode.Abandon,
            ContinueOnError: false,
            DryRun: false,
            MaxMessages: null);

        await using var mover = new ServiceBusMessageMover(
            fixture.GetDataConnectionString(),
            null,
            source,
            target,
            behavior,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-mover");

        using var cts = new CancellationTokenSource(TestTimeout);
        var successes = 0;
        try
        {
            await foreach (var result in mover.MoveMessagesAsync(cts.Token))
            {
                if (!result.IsSuccess)
                {
                    Assert.Fail($"Mover returned a failure result: {string.Join("; ", result.Errors.Select(e => e.Message))}");
                }

                successes++;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Assert.Fail($"Timed out after {TestTimeout.TotalSeconds:N0}s while moving queue-to-queue messages.");
        }

        Assert.Equal(3, successes);
        var targetReceived = await fixture.DrainQueueAsync(targetQueue, timeout: TimeSpan.FromSeconds(10));
        var sourceReceived = await fixture.DrainQueueAsync(sourceQueue, timeout: TimeSpan.FromSeconds(3));
        Assert.Equal(3, targetReceived);
        Assert.Equal(0, sourceReceived);
    }

    [Fact]
    public async Task MoveMessagesAsync_FromDeadLetterSource_MovesDeadLetterMessage()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-mover-dlq-source");
        var targetQueue = await fixture.CreateQueueAsync("it-mover-dlq-target");
        await fixture.SendMessagesAsync(sourceQueue, 1);
        await fixture.DeadLetterOneMessageAsync(sourceQueue);

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.DeadLetter);
        var target = new TargetEndpoint(targetQueue);
        var behavior = new MessageMoverBehavior(
            SettlementMode.Complete,
            SettlementMode.Abandon,
            ContinueOnError: false,
            DryRun: false,
            MaxMessages: null);

        await using var mover = new ServiceBusMessageMover(
            fixture.GetDataConnectionString(),
            null,
            source,
            target,
            behavior,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-mover-dlq");

        using var cts = new CancellationTokenSource(TestTimeout);
        var successes = 0;
        try
        {
            await foreach (var result in mover.MoveMessagesAsync(cts.Token))
            {
                if (!result.IsSuccess)
                {
                    Assert.Fail($"Mover returned a failure result: {string.Join("; ", result.Errors.Select(e => e.Message))}");
                }

                successes++;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Assert.Fail($"Timed out after {TestTimeout.TotalSeconds:N0}s while moving dead-letter messages.");
        }

        Assert.Equal(1, successes);
        var targetReceived = await fixture.DrainQueueAsync(targetQueue, timeout: TimeSpan.FromSeconds(10));
        var sourceDeadLetterReceived = await fixture.DrainQueueAsync(
            sourceQueue,
            SourceSubQueue.DeadLetter,
            TimeSpan.FromSeconds(5));
        Assert.Equal(1, targetReceived);
        Assert.Equal(0, sourceDeadLetterReceived);
    }

    [Fact]
    public async Task MoveMessagesAsync_WithDryRun_DoesNotSendToTarget()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-mover-dry-source");
        var targetQueue = await fixture.CreateQueueAsync("it-mover-dry-target");
        await fixture.SendMessagesAsync(sourceQueue, 1);

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.None);
        var target = new TargetEndpoint(targetQueue);
        var behavior = new MessageMoverBehavior(
            SettlementMode.Abandon,
            SettlementMode.Abandon,
            ContinueOnError: false,
            DryRun: true,
            MaxMessages: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var mover = new ServiceBusMessageMover(
            fixture.GetDataConnectionString(),
            null,
            source,
            target,
            behavior,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-mover-dry");

        await foreach (var result in mover.MoveMessagesAsync(cts.Token))
        {
            Assert.True(result.IsSuccess, $"Dry-run returned failure: {string.Join("; ", result.Errors.Select(e => e.Message))}");
            break;
        }

        var targetReceived = await fixture.DrainQueueAsync(targetQueue, timeout: TimeSpan.FromSeconds(5));
        var sourceReceived = await fixture.DrainQueueAsync(sourceQueue, timeout: TimeSpan.FromSeconds(10));
        Assert.Equal(0, targetReceived);
        Assert.Equal(1, sourceReceived);
    }

    [Fact]
    public async Task MoveMessagesAsync_WhenFirstReceiveIsEmpty_ContinuesAndMovesLaterMessages()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-mover-empty-first-source");
        var targetQueue = await fixture.CreateQueueAsync("it-mover-empty-first-target");

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.None);
        var target = new TargetEndpoint(targetQueue);
        var behavior = new MessageMoverBehavior(
            SettlementMode.Complete,
            SettlementMode.Abandon,
            ContinueOnError: false,
            DryRun: false,
            MaxMessages: null);

        await using var mover = new ServiceBusMessageMover(
            fixture.GetDataConnectionString(),
            null,
            source,
            target,
            behavior,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-mover-empty-first");

        using var cts = new CancellationTokenSource(TestTimeout);
        var results = new List<FluentResults.Result>();
        var moveTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var result in mover.MoveMessagesAsync(cts.Token))
                {
                    results.Add(result);
                    if (results.Count >= 1)
                    {
                        cts.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Expected after first successful move.
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
        await fixture.SendMessagesAsync(sourceQueue, 1);
        await moveTask;

        Assert.Single(results);
        Assert.True(results[0].IsSuccess, string.Join("; ", results[0].Errors.Select(e => e.Message)));
        var targetReceived = await fixture.DrainQueueAsync(targetQueue, timeout: TimeSpan.FromSeconds(10));
        Assert.Equal(1, targetReceived);
    }
}
