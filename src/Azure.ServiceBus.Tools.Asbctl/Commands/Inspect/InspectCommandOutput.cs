using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;

public sealed record InspectCommandOutput : CommandOutput
{
    public required InspectedEntityKind Kind { get; init; }
    public required string Path { get; init; }
    public required string Authentication { get; init; }
    public QueueProperties? QueueProperties { get; init; }
    public QueueRuntimeProperties? QueueRuntimeProperties { get; init; }
    public TopicProperties? TopicProperties { get; init; }
    public TopicRuntimeProperties? TopicRuntimeProperties { get; init; }
    public SubscriptionProperties? SubscriptionProperties { get; init; }
    public SubscriptionRuntimeProperties? SubscriptionRuntimeProperties { get; init; }
}
