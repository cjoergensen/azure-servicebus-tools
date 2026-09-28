using Cjoergensen.Azure.ServiceBus.Tools.Purger;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.IntegrationTests;

[Collection(ServiceBusCollection.Name)]
[Trait("Category", "Integration")]
public class PurgerIntegrationTests(ServiceBusIntegrationFixture fixture)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task PurgeMessagesAsync_QueueSource_CompletesAllMessages()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-purger-source");
        await fixture.SendMessagesAsync(sourceQueue, 3);

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.None);
        await using var purger = new ServiceBusMessagePurger(
            fixture.GetDataConnectionString(),
            null,
            source,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-purger");

        using var cts = new CancellationTokenSource(TestTimeout);
        var successes = 0;
        try
        {
            await foreach (var result in purger.PurgeMessagesAsync(cts.Token))
            {
                if (!result.IsSuccess)
                {
                    Assert.Fail($"Purger returned a failure result: {string.Join("; ", result.Errors.Select(e => e.Message))}");
                }

                successes++;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Assert.Fail($"Timed out after {TestTimeout.TotalSeconds:N0}s while purging queue messages.");
        }

        Assert.Equal(3, successes);
        var sourceReceived = await fixture.DrainQueueAsync(sourceQueue, timeout: TimeSpan.FromSeconds(5));
        Assert.Equal(0, sourceReceived);
    }

    [Fact]
    public async Task PurgeMessagesAsync_DeadLetterSource_CompletesDeadLetterMessages()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-purger-dlq-source");
        await fixture.SendMessagesAsync(sourceQueue, 1);
        await fixture.DeadLetterOneMessageAsync(sourceQueue);

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.DeadLetter);
        await using var purger = new ServiceBusMessagePurger(
            fixture.GetDataConnectionString(),
            null,
            source,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-purger-dlq");

        using var cts = new CancellationTokenSource(TestTimeout);
        var successes = 0;
        try
        {
            await foreach (var result in purger.PurgeMessagesAsync(cts.Token))
            {
                if (!result.IsSuccess)
                {
                    Assert.Fail($"Purger returned a failure result: {string.Join("; ", result.Errors.Select(e => e.Message))}");
                }

                successes++;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Assert.Fail($"Timed out after {TestTimeout.TotalSeconds:N0}s while purging dead-letter messages.");
        }

        Assert.Equal(1, successes);
        var deadLetterReceived = await fixture.DrainQueueAsync(
            sourceQueue,
            SourceSubQueue.DeadLetter,
            TimeSpan.FromSeconds(5));
        Assert.Equal(0, deadLetterReceived);
    }
}
