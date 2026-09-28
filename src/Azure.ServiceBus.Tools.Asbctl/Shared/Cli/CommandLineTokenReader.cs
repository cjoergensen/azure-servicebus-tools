namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public sealed class CommandLineTokenReader(string[] args)
{
    private int index = -1;

    public bool TryReadNext(out string token)
    {
        if (index + 1 >= args.Length)
        {
            token = string.Empty;
            return false;
        }

        token = args[++index];
        return true;
    }

    public string ReadRequiredValue(string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        return args[++index];
    }
}
