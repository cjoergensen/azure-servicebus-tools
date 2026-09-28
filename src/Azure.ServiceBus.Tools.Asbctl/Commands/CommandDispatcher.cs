using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl.Commands;

public sealed class CommandDispatcher(IEnumerable<IAsbCommand> commands)
{
    private readonly Dictionary<string, IAsbCommand> commandsByName = commands
        .ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryResolve(string token, out IAsbCommand command)
    {
        return commandsByName.TryGetValue(token, out command!);
    }
}
