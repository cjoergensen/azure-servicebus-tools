using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.Expedite;

public sealed record ExpediteCommandOutput : CommandOutput
{
    public required string Source { get; init; }
    public required int MessagesProcessed { get; init; }
    public required int MessagesExpedited { get; init; }
    public required int MessagesFailed { get; init; }
    public required double AverageMessagesPerSecond { get; init; }
    public required string Goal { get; init; }
    public required bool GoalReached { get; init; }
    public required string Status { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime CompletedAt { get; init; }
    public required string Authentication { get; init; }
    public required string Selection { get; init; }
    public required string? MessageId { get; init; }
    public required int PeekBatchSize { get; init; }
    public required int PrefetchCount { get; init; }
    public required bool ContinueOnError { get; init; }
}
