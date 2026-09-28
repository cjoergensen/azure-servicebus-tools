using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;

public sealed class ServiceBusEntityInspector(string? connectionString, string? serviceBusNamespace)
{
    private readonly global::Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient adminClient =
        ServiceBusClientFactory.CreateAdministrationClient(connectionString, serviceBusNamespace);

    public async Task<InspectResult> InspectAsync(InspectEndpoint endpoint)
    {
        if (endpoint.Kind == InspectEntityKind.Subscription)
        {
            var subscriptionExists = await adminClient.SubscriptionExistsAsync(endpoint.EntityName, endpoint.SubscriptionName!);
            if (!subscriptionExists)
            {
                return new InspectResult(InspectedEntityKind.NotFound, endpoint);
            }

            var subscription = await adminClient.GetSubscriptionAsync(endpoint.EntityName, endpoint.SubscriptionName!);
            var runtime = await adminClient.GetSubscriptionRuntimePropertiesAsync(endpoint.EntityName, endpoint.SubscriptionName!);
            return new InspectResult(
                InspectedEntityKind.Subscription,
                endpoint,
                SubscriptionProperties: subscription.Value,
                SubscriptionRuntimeProperties: runtime.Value);
        }

        if (await adminClient.QueueExistsAsync(endpoint.EntityName))
        {
            var queue = await adminClient.GetQueueAsync(endpoint.EntityName);
            var runtime = await adminClient.GetQueueRuntimePropertiesAsync(endpoint.EntityName);
            return new InspectResult(
                InspectedEntityKind.Queue,
                endpoint,
                QueueProperties: queue.Value,
                QueueRuntimeProperties: runtime.Value);
        }

        if (await adminClient.TopicExistsAsync(endpoint.EntityName))
        {
            var topic = await adminClient.GetTopicAsync(endpoint.EntityName);
            var runtime = await adminClient.GetTopicRuntimePropertiesAsync(endpoint.EntityName);
            return new InspectResult(
                InspectedEntityKind.Topic,
                endpoint,
                TopicProperties: topic.Value,
                TopicRuntimeProperties: runtime.Value);
        }

        return new InspectResult(InspectedEntityKind.NotFound, endpoint);
    }
}
