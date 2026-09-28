using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityLister;

public sealed record ListCommandOutput : CommandOutput
{
    public required ListEntityKind Kind { get; init; }
    public required string? TopicName { get; init; }
    public required IReadOnlyList<QueueListEntry> Queues { get; init; }
    public required IReadOnlyList<TopicListEntry> Topics { get; init; }
    public required IReadOnlyList<SubscriptionListEntry> Subscriptions { get; init; }
    public required string Authentication { get; init; }
}
