namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

public interface IAsbCommand
{
    string Name { get; }
    string Description { get; }
}

public interface IAsbCommand<TOutput> : IAsbCommand where TOutput : CommandOutput
{
    IAsyncEnumerable<CommandUpdate> ExecuteAsync(string[] args, CancellationToken ct = default);
}
