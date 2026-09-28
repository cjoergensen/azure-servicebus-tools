namespace Cjoergensen.Azure.ServiceBus.Tools.Purger;

public sealed record PurgerBehavior(
    bool ContinueOnError,
    int? MaxMessages);
