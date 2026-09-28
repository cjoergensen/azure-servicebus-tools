namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class SharedCommandOptions
{
    public static bool TryParseAuthenticationOption(
        CommandLineTokenReader reader,
        string argument,
        ref string? connectionString,
        ref string? serviceBusNamespace)
    {
        switch (argument.ToLowerInvariant())
        {
            case "-c":
            case "--connection-string":
                connectionString = reader.ReadRequiredValue(argument);
                return true;
            case "--namespace":
                serviceBusNamespace = CommandLineParsing.NormalizeNamespace(reader.ReadRequiredValue(argument));
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseWorkerOptions(
        CommandLineTokenReader reader,
        string argument,
        ref int maxConcurrency,
        ref int maxMessagesPerBatch,
        ref int prefetchCount,
        bool allowWorkers = true,
        bool allowBatchSize = true)
    {
        switch (argument.ToLowerInvariant())
        {
            case "--workers" when allowWorkers:
                maxConcurrency = CommandLineParsing.ParsePositiveInt(reader.ReadRequiredValue(argument), "max concurrency");
                return true;
            case "--receive-batch-size" when allowBatchSize:
                maxMessagesPerBatch = CommandLineParsing.ParsePositiveInt(reader.ReadRequiredValue(argument), "max messages per batch");
                return true;
            case "--prefetch-count":
                prefetchCount = CommandLineParsing.ParseNonNegativeInt(reader.ReadRequiredValue(argument), "prefetch count");
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseClientIdentifierOption(
        CommandLineTokenReader reader,
        string argument,
        ref string? identifier)
    {
        if (!argument.Equals("--client-id-prefix", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        identifier = reader.ReadRequiredValue(argument);
        return true;
    }

    public static bool TryParseOutputOption(
        CommandLineTokenReader reader,
        string argument,
        ref OutputFormat outputFormat)
    {
        if (!argument.Equals("--output", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = reader.ReadRequiredValue(argument);
        outputFormat = value.ToLowerInvariant() switch
        {
            "json" => OutputFormat.Json,
            "tsv" => OutputFormat.Tsv,
            _ => throw new ArgumentException($"Unknown output format '{value}'. Use 'json' or 'tsv'.")
        };
        return true;
    }
}
