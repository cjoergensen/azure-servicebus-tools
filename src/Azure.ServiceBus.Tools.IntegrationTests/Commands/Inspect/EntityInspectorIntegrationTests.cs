using Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.IntegrationTests;

[Collection(ServiceBusCollection.Name)]
[Trait("Category", "Integration")]
public class EntityInspectorIntegrationTests(ServiceBusIntegrationFixture fixture)
{
    [Fact]
    public async Task InspectAsync_WithQueuePath_ReturnsQueuePropertiesAndRuntime()
    {
        var queueName = await fixture.CreateQueueAsync("it-inspector-queue");

        var inspector = new ServiceBusEntityInspector(fixture.GetManagementConnectionString(), null);
        var endpoint = new InspectEndpoint(InspectEntityKind.Entity, queueName, null, SourceSubQueue.None);

        var result = await inspector.InspectAsync(endpoint);

        Assert.Equal(InspectedEntityKind.Queue, result.Kind);
        Assert.NotNull(result.QueueProperties);
        Assert.NotNull(result.QueueRuntimeProperties);
        Assert.Equal(queueName, result.QueueProperties!.Name);
        Assert.Equal(queueName, result.QueueRuntimeProperties!.Name);
        Assert.True(result.QueueRuntimeProperties.ActiveMessageCount >= 0);
    }

    [Fact]
    public async Task InspectAsync_WithTopicPath_ReturnsTopicPropertiesAndRuntime()
    {
        var (topicName, _) = await fixture.CreateSubscriptionAsync("it-inspector-topic", "it-inspector-sub");
        var inspector = new ServiceBusEntityInspector(fixture.GetManagementConnectionString(), null);
        var endpoint = new InspectEndpoint(InspectEntityKind.Entity, topicName, null, SourceSubQueue.None);

        var result = await inspector.InspectAsync(endpoint);

        Assert.Equal(InspectedEntityKind.Topic, result.Kind);
        Assert.NotNull(result.TopicProperties);
        Assert.NotNull(result.TopicRuntimeProperties);
        Assert.Equal(topicName, result.TopicProperties!.Name);
        Assert.Equal(topicName, result.TopicRuntimeProperties!.Name);
        Assert.True(result.TopicRuntimeProperties.SubscriptionCount >= 0);
    }

    [Fact]
    public async Task InspectAsync_WithSubscriptionPath_ReturnsSubscriptionPropertiesAndRuntime()
    {
        var (topicName, subscriptionName) = await fixture.CreateSubscriptionAsync("it-inspector-sub-topic", "it-inspector-subscription");
        var inspector = new ServiceBusEntityInspector(fixture.GetManagementConnectionString(), null);
        var endpoint = new InspectEndpoint(InspectEntityKind.Subscription, topicName, subscriptionName, SourceSubQueue.None);

        var result = await inspector.InspectAsync(endpoint);

        Assert.Equal(InspectedEntityKind.Subscription, result.Kind);
        Assert.NotNull(result.SubscriptionProperties);
        Assert.NotNull(result.SubscriptionRuntimeProperties);
        Assert.Equal(topicName, result.SubscriptionProperties!.TopicName);
        Assert.Equal(subscriptionName, result.SubscriptionProperties.SubscriptionName);
        Assert.Equal(topicName, result.SubscriptionRuntimeProperties!.TopicName);
        Assert.Equal(subscriptionName, result.SubscriptionRuntimeProperties.SubscriptionName);
    }
}
