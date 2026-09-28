using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

public abstract record CommandUpdate;

public sealed record OperationHeader(
    string CommandLine,
    string Config,
    DateTime StartedAt,
    long? TotalMessages,
    OutputFormat OutputFormat) : CommandUpdate;

public sealed record OperationProgress(int Processed, int Succeeded, int Failed) : CommandUpdate;

public sealed record OperationError(string Message) : CommandUpdate;
