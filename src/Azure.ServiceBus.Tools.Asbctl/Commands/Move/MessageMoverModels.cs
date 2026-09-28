namespace Cjoergensen.Azure.ServiceBus.Tools.MessageMover;

public enum SettlementMode
{
    Complete,
    Abandon,
    DeadLetter,
    None
}

public sealed record TargetEndpoint(string EntityName)
{
    public string DisplayName => EntityName;
}

public sealed record MessageMoverBehavior(
    SettlementMode OnSuccess,
    SettlementMode OnFailure,
    bool ContinueOnError,
    bool DryRun,
    int? MaxMessages);
