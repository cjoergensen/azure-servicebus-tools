namespace Cjoergensen.Azure.ServiceBus.Tools.Schedule;

public sealed record ScheduleBehavior(
    bool ScheduleAll,
    string? MessageId,
    DateTimeOffset ScheduleAtUtc,
    bool ContinueOnError);

public sealed record ScheduleOperationResult(
    bool IsSuccess,
    string MessageId,
    string? ErrorMessage = null);
