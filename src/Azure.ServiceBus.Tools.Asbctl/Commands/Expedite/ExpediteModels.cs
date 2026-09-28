namespace Cjoergensen.Azure.ServiceBus.Tools.Expedite;

public sealed record ExpediteBehavior(
    bool ExpediteAll,
    string? MessageId,
    bool ContinueOnError);

public sealed record ExpediteOperationResult(
    bool IsSuccess,
    string MessageId,
    string? ErrorMessage = null);
