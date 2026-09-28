using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.IntegrationTests;

[Collection(ServiceBusCollection.Name)]
[Trait("Category", "Integration")]
public class AutoForwardResolutionIntegrationTests(ServiceBusIntegrationFixture fixture)
{
    [Fact]
    public async Task ResolveAutoForwardSourceAsync_WithoutForwarding_ReturnsOriginalSource()
    {
        var sourceQueue = await fixture.CreateQueueAsync("it-forward-none");
        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.None);
        var admin = new ServiceBusAdminClient(fixture.GetManagementConnectionString(), null);

        var resolution = await admin.ResolveAutoForwardSourceAsync(source);

        Assert.False(resolution.IsAutoForwarded);
        Assert.Null(resolution.Message);
        Assert.Equal(source, resolution.EffectiveSource);
    }

    [Fact]
    public async Task ResolveAutoForwardSourceAsync_QueueForwardToQueue_ResolvesEffectiveQueueSource()
    {
        var forwardingQueue = await fixture.CreateQueueAsync("it-forward-target");
        var sourceQueue = await fixture.CreateQueueAsync(
            "it-forward-source",
            o => o.ForwardTo = forwardingQueue);

        var source = new SourceEndpoint(SourceEntityKind.Queue, sourceQueue, null, SourceSubQueue.None);
        var admin = new ServiceBusAdminClient(fixture.GetManagementConnectionString(), null);

        var resolution = await admin.ResolveAutoForwardSourceAsync(source);

        Assert.True(resolution.IsAutoForwarded);
        Assert.NotNull(resolution.Message);
        Assert.Equal(SourceEntityKind.Queue, resolution.EffectiveSource.Kind);
        Assert.Equal(forwardingQueue, resolution.EffectiveSource.EntityName);
        Assert.Equal(SourceSubQueue.None, resolution.EffectiveSource.SubQueue);
    }

    [Fact]
    public async Task ResolveAutoForwardSourceAsync_SubscriptionDeadLetterForwardToQueue_ResolvesEffectiveQueueSource()
    {
        var forwardingQueue = await fixture.CreateQueueAsync("it-forward-subtarget");
        var (topic, subscription) = await fixture.CreateSubscriptionAsync(
            "it-forward-topic",
            "it-forward-sub",
            o => o.ForwardDeadLetteredMessagesTo = forwardingQueue);

        var source = new SourceEndpoint(SourceEntityKind.Subscription, topic, subscription, SourceSubQueue.DeadLetter);
        var admin = new ServiceBusAdminClient(fixture.GetManagementConnectionString(), null);

        var resolution = await admin.ResolveAutoForwardSourceAsync(source);

        Assert.True(resolution.IsAutoForwarded);
        Assert.NotNull(resolution.Message);
        Assert.Equal(SourceEntityKind.Queue, resolution.EffectiveSource.Kind);
        Assert.Equal(forwardingQueue, resolution.EffectiveSource.EntityName);
    }
}
