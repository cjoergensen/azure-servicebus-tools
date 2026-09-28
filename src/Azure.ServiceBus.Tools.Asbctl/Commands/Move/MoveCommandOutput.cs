using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.MessageMover;

public sealed record MoveCommandOutput : CommandOutput
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required int MessagesProcessed { get; init; }
    public required int MessagesMoved { get; init; }
    public required int MessagesFailed { get; init; }
    public required double AverageMessagesPerSecond { get; init; }
    public required string Goal { get; init; }
    public required bool GoalReached { get; init; }
    public required string Status { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime CompletedAt { get; init; }
    public required string Authentication { get; init; }
    public required int Workers { get; init; }
    public required int ReceiveBatchSize { get; init; }
    public required int PrefetchCount { get; init; }
    public required int MaxIdleMinutes { get; init; }
    public required string SuccessSettlement { get; init; }
    public required string FailureSettlement { get; init; }
    public required bool DryRun { get; init; }
    public required bool ContinueOnError { get; init; }
}
