using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

public abstract record CommandOutput : CommandUpdate
{
    public required OutputFormat RequestedFormat { get; init; }
}
