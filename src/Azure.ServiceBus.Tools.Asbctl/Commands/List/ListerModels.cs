using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityLister;

public enum ListEntityKind
{
    Queues,
    Topics,
    Subscriptions
}

public sealed record QueueListEntry(
    string Name,
    EntityStatus Status,
    long ActiveMessageCount,
    long DeadLetterMessageCount,
    long ScheduledMessageCount,
    long TotalMessageCount,
    long SizeInBytes,
    long MaxSizeInMegabytes);

public sealed record TopicListEntry(
    string Name,
    EntityStatus Status,
    int SubscriptionCount,
    long ScheduledMessageCount,
    long SizeInBytes,
    long MaxSizeInMegabytes);

public sealed record SubscriptionListEntry(
    string Name,
    EntityStatus Status,
    long ActiveMessageCount,
    long DeadLetterMessageCount,
    long TransferDeadLetterMessageCount,
    long TotalMessageCount,
    int MaxDeliveryCount,
    TimeSpan LockDuration);

public sealed record ListResult(
    ListEntityKind Kind,
    string? TopicName,
    IReadOnlyList<QueueListEntry> Queues,
    IReadOnlyList<TopicListEntry> Topics,
    IReadOnlyList<SubscriptionListEntry> Subscriptions)
{
    public static ListResult ForQueues(IReadOnlyList<QueueListEntry> queues) =>
        new(ListEntityKind.Queues, null, queues, [], []);

    public static ListResult ForTopics(IReadOnlyList<TopicListEntry> topics) =>
        new(ListEntityKind.Topics, null, [], topics, []);

    public static ListResult ForSubscriptions(string topicName, IReadOnlyList<SubscriptionListEntry> subscriptions) =>
        new(ListEntityKind.Subscriptions, topicName, [], [], subscriptions);
}
