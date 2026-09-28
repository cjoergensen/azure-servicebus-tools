using Cjoergensen.Azure.ServiceBus.Tools.Schedule;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.IntegrationTests;

[Collection(ServiceBusCollection.Name)]
[Trait("Category", "Integration")]
public class ScheduleIntegrationTests(ServiceBusIntegrationFixture fixture)
{
    [Fact]
    public async Task ScheduleMessagesAsync_WithAll_ReschedulesAllMessagesAsScheduled()
    {
        var queueName = await fixture.CreateQueueAsync("it-schedule-all");
        await fixture.SendMessagesAsync(queueName, 2);

        var source = new SourceEndpoint(SourceEntityKind.Queue, queueName, null, SourceSubQueue.None);
        var behavior = new ScheduleBehavior(
            ScheduleAll: true,
            MessageId: null,
            ScheduleAtUtc: DateTimeOffset.UtcNow.AddSeconds(5),
            ContinueOnError: false);

        await using var scheduler = new ServiceBusMessageScheduler(
            fixture.GetDataConnectionString(),
            null,
            source,
            behavior,
            maxMessagesPerBatch: SharedConstants.DefaultMaxMessagesPerBatch,
            prefetchCount: 0,
            identifier: "it-schedule-all");

        var results = new List<ScheduleOperationResult>();
        await foreach (var result in scheduler.ScheduleMessagesAsync(CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccess, r.ErrorMessage));
        await Task.Delay(TimeSpan.FromSeconds(7));
        var receivedAfterSchedule = await fixture.DrainQueueAsync(queueName, timeout: TimeSpan.FromSeconds(12));
        Assert.Equal(2, receivedAfterSchedule);
    }

    [Fact]
    public async Task ScheduleMessagesAsync_WithMessageId_ReschedulesOnlyMatchingMessage()
    {
        var queueName = await fixture.CreateQueueAsync("it-schedule-one");
        await fixture.SendMessagesAsync(queueName, 2);

        var targetMessageId = $"{queueName}-1";
        var source = new SourceEndpoint(SourceEntityKind.Queue, queueName, null, SourceSubQueue.None);
        var behavior = new ScheduleBehavior(
            ScheduleAll: false,
            MessageId: targetMessageId,
            ScheduleAtUtc: DateTimeOffset.UtcNow.AddSeconds(5),
            ContinueOnError: false);

        await using var scheduler = new ServiceBusMessageScheduler(
            fixture.GetDataConnectionString(),
            null,
            source,
            behavior,
            maxMessagesPerBatch: 1,
            prefetchCount: 0,
            identifier: "it-schedule-one",
            initialSourceMessageCount: 2);

        var results = new List<ScheduleOperationResult>();
        await foreach (var result in scheduler.ScheduleMessagesAsync(CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.Single(results);
        Assert.True(results[0].IsSuccess, results[0].ErrorMessage);
        Assert.Equal(targetMessageId, results[0].MessageId);
        var immediatelyAvailable = await fixture.DrainQueueAsync(queueName, timeout: TimeSpan.FromSeconds(2));
        Assert.Equal(1, immediatelyAvailable);
        await Task.Delay(TimeSpan.FromSeconds(7));
        var scheduledNowAvailable = await fixture.DrainQueueAsync(queueName, timeout: TimeSpan.FromSeconds(12));
        Assert.Equal(1, scheduledNowAvailable);
    }
}
