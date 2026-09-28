namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public enum SourceEntityKind
{
    Queue,
    Subscription
}

public enum SourceSubQueue
{
    None,
    DeadLetter,
    TransferDeadLetter
}

public sealed record SourceEndpoint(
    SourceEntityKind Kind,
    string EntityName,
    string? SubscriptionName,
    SourceSubQueue SubQueue)
{
    public string DisplayName
    {
        get
        {
            var basePath = Kind == SourceEntityKind.Queue
                ? EntityName
                : $"{EntityName}/Subscriptions/{SubscriptionName}";

            return SubQueue switch
            {
                SourceSubQueue.None => basePath,
                SourceSubQueue.DeadLetter => $"{basePath}/$DeadLetterQueue",
                SourceSubQueue.TransferDeadLetter => $"{basePath}/$Transfer/$DeadLetterQueue",
                _ => basePath
            };
        }
    }
}
