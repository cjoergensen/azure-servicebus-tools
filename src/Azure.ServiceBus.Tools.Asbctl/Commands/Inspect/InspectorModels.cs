namespace Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;

using Cjoergensen.Azure.ServiceBus.Tools.Shared;

public enum InspectEntityKind
{
    Entity,
    Subscription
}

public enum InspectedEntityKind
{
    Queue,
    Topic,
    Subscription,
    NotFound
}

public sealed record InspectEndpoint(
    InspectEntityKind Kind,
    string EntityName,
    string? SubscriptionName,
    SourceSubQueue SubQueue)
{
    public string DisplayName => Kind switch
    {
        InspectEntityKind.Subscription => AppendSubQueueSuffix($"{EntityName}/Subscriptions/{SubscriptionName}", SubQueue),
        _ => AppendSubQueueSuffix(EntityName, SubQueue)
    };

    private static string AppendSubQueueSuffix(string basePath, SourceSubQueue subQueue)
        => subQueue switch
        {
            SourceSubQueue.None => basePath,
            SourceSubQueue.DeadLetter => $"{basePath}/$DeadLetterQueue",
            SourceSubQueue.TransferDeadLetter => $"{basePath}/$Transfer/$DeadLetterQueue",
            _ => basePath
        };
}

public sealed record InspectResult(
    InspectedEntityKind Kind,
    InspectEndpoint Endpoint,
    global::Azure.Messaging.ServiceBus.Administration.QueueProperties? QueueProperties = null,
    global::Azure.Messaging.ServiceBus.Administration.QueueRuntimeProperties? QueueRuntimeProperties = null,
    global::Azure.Messaging.ServiceBus.Administration.TopicProperties? TopicProperties = null,
    global::Azure.Messaging.ServiceBus.Administration.TopicRuntimeProperties? TopicRuntimeProperties = null,
    global::Azure.Messaging.ServiceBus.Administration.SubscriptionProperties? SubscriptionProperties = null,
    global::Azure.Messaging.ServiceBus.Administration.SubscriptionRuntimeProperties? SubscriptionRuntimeProperties = null);
