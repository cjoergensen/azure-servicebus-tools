using Cjoergensen.Azure.ServiceBus.Tools.Expedite;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.IntegrationTests;

[Collection(ServiceBusCollection.Name)]
[Trait("Category", "Integration")]
public class ExpediteIntegrationTests(ServiceBusIntegrationFixture fixture)
{
    [Fact]
    public async Task ExpediteMessagesAsync_WithMessageId_MakesScheduledMessageAvailableNow()
    {
        var queueName = await fixture.CreateQueueAsync("it-expedite-single");
        await fixture.ScheduleMessagesAsync(queueName, 1, DateTimeOffset.UtcNow.AddHours(1));
        var messageId = $"{queueName}-scheduled-1";

        var source = new SourceEndpoint(SourceEntityKind.Queue, queueName, null, SourceSubQueue.None);
        var behavior = new ExpediteBehavior(ExpediteAll: false, MessageId: messageId, ContinueOnError: false);
        await using var expediter = new ServiceBusMessageExpediter(
            fixture.GetDataConnectionString(),
            null,
            source,
            behavior,
            peekBatchSize: 50,
            prefetchCount: 0,
            identifier: "it-expedite-single");

        var results = new List<ExpediteOperationResult>();
        await foreach (var result in expediter.ExpediteMessagesAsync(CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.Single(results);
        Assert.True(results[0].IsSuccess, results[0].ErrorMessage);

        var received = await fixture.DrainQueueAsync(queueName, timeout: TimeSpan.FromSeconds(10));
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task ExpediteMessagesAsync_WithAll_MakesAllScheduledMessagesAvailableNow()
    {
        var queueName = await fixture.CreateQueueAsync("it-expedite-all");
        await fixture.ScheduleMessagesAsync(queueName, 2, DateTimeOffset.UtcNow.AddHours(1));

        var source = new SourceEndpoint(SourceEntityKind.Queue, queueName, null, SourceSubQueue.None);
        var behavior = new ExpediteBehavior(ExpediteAll: true, MessageId: null, ContinueOnError: false);
        await using var expediter = new ServiceBusMessageExpediter(
            fixture.GetDataConnectionString(),
            null,
            source,
            behavior,
            peekBatchSize: 50,
            prefetchCount: 0,
            identifier: "it-expedite-all");

        var results = new List<ExpediteOperationResult>();
        await foreach (var result in expediter.ExpediteMessagesAsync(CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccess, r.ErrorMessage));

        var received = await fixture.DrainQueueAsync(queueName, timeout: TimeSpan.FromSeconds(10));
        Assert.Equal(2, received);
    }
}
