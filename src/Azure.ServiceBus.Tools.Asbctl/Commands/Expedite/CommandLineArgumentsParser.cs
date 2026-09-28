using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Expedite;

public static class CommandLineArgumentsParser
{
    public class ArgumentParsingResult
    {
        public bool ShowHelp { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? ServiceBusNamespace { get; init; }
        public SourceEndpoint? Source { get; init; }
        public ExpediteBehavior? Behavior { get; init; }
        public int PeekBatchSize { get; init; }
        public int PrefetchCount { get; init; }
        public string Identifier { get; init; } = "";
        public OutputFormat OutputFormat { get; init; } = OutputFormat.Tsv;
    }

    public static ArgumentParsingResult ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return new ArgumentParsingResult { ShowHelp = true };
        }

        string? connectionString = null;
        string? serviceBusNamespace = null;
        string? sourcePath = null;
        string? messageId = null;
        var expediteAll = false;
        var continueOnError = false;
        var ignoredMaxConcurrency = SharedConstants.DefaultMaxConcurrency;
        var peekBatchSize = Constants.DefaultPeekBatchSize;
        var prefetchCount = 0;
        string? identifier = null;
        var outputFormat = OutputFormat.Tsv;
        var positional = new List<string>();

        try
        {
            var reader = new CommandLineTokenReader(args);
            while (reader.TryReadNext(out var arg))
            {
                if (!arg.StartsWith("-", StringComparison.Ordinal))
                {
                    positional.Add(arg);
                    continue;
                }

                if (SharedCommandOptions.TryParseAuthenticationOption(reader, arg, ref connectionString, ref serviceBusNamespace))
                {
                    continue;
                }

                if (SharedCommandOptions.TryParseWorkerOptions(reader, arg, ref ignoredMaxConcurrency, ref peekBatchSize, ref prefetchCount, allowWorkers: false, allowBatchSize: false))
                {
                    continue;
                }

                if (SharedCommandOptions.TryParseClientIdentifierOption(reader, arg, ref identifier))
                {
                    continue;
                }

                if (SharedCommandOptions.TryParseOutputOption(reader, arg, ref outputFormat))
                {
                    continue;
                }

                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                        return new ArgumentParsingResult { ShowHelp = true };
                    case "--source":
                        sourcePath = reader.ReadRequiredValue(arg);
                        break;
                    case "--message-id":
                        messageId = reader.ReadRequiredValue(arg);
                        break;
                    case "--all":
                        expediteAll = true;
                        break;
                    case "--continue-on-failure":
                        continueOnError = true;
                        break;
                    case "--peek-batch-size":
                        peekBatchSize = CommandLineParsing.ParsePositiveInt(reader.ReadRequiredValue(arg), "peek batch size");
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            ApplyPositionalArguments(positional, ref sourcePath);
            var source = CommandLineParsing.ParseSourcePath(
                sourcePath,
                "Source path is required: use expedite <source-path> or --source.");

            var behavior = BuildBehavior(expediteAll, messageId, continueOnError);
            ValidateArguments(connectionString, serviceBusNamespace, source, peekBatchSize, prefetchCount);

            return new ArgumentParsingResult
            {
                ShowHelp = false,
                ServiceBusConnectionString = connectionString,
                ServiceBusNamespace = serviceBusNamespace,
                Source = source,
                Behavior = behavior,
                PeekBatchSize = peekBatchSize,
                PrefetchCount = prefetchCount,
                Identifier = string.IsNullOrWhiteSpace(identifier) ? Constants.DefaultIdentifier : identifier,
                OutputFormat = outputFormat
            };
        }
        catch (ArgumentException ex)
        {
            return new ArgumentParsingResult { ShowHelp = true, ErrorMessage = ex.Message };
        }
    }

    private static void ApplyPositionalArguments(List<string> positional, ref string? sourcePath)
    {
        if (positional.Count == 0)
        {
            return;
        }

        var tokens = CommandLineValidation.ResolveCommandOperands(positional, "expedite");

        if (tokens.Count == 0)
        {
            return;
        }

        CommandLineValidation.EnsureExpectedOperandCount(
            tokens,
            expectedCount: 1,
            usageError: "Usage requires exactly one source path operand: expedite <source-path>.");
        CommandLineValidation.EnsureNotMixedWithOptionValue(
            !string.IsNullOrWhiteSpace(sourcePath),
            "Specify source with either positional path or --source, not both.");

        sourcePath = tokens[0];
    }

    private static ExpediteBehavior BuildBehavior(bool expediteAll, string? messageId, bool continueOnError)
    {
        if (expediteAll == !string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Specify exactly one of --all or --message-id <id>.");
        }

        return new ExpediteBehavior(expediteAll, messageId, continueOnError);
    }

    private static void ValidateArguments(
        string? connectionString,
        string? serviceBusNamespace,
        SourceEndpoint source,
        int peekBatchSize,
        int prefetchCount)
    {
        CommandLineValidation.ValidateAuthentication(connectionString, serviceBusNamespace);
        CommandLineValidation.ValidateSourcePathCompatibility(source);

        if (source.Kind != SourceEntityKind.Queue || source.SubQueue != SourceSubQueue.None)
        {
            throw new ArgumentException("Expedite currently supports primary queue sources only (no subscriptions or subqueues).");
        }

        if (peekBatchSize <= 0)
        {
            throw new ArgumentException("Peek batch size must be a positive number.");
        }

        if (peekBatchSize > SharedConstants.MaxAllowedMessagesPerBatch)
        {
            throw new ArgumentException($"Peek batch size cannot exceed {SharedConstants.MaxAllowedMessagesPerBatch}.");
        }

        if (prefetchCount < 0)
        {
            throw new ArgumentException("Prefetch count must be zero or greater.");
        }
    }

    public static void PrintUsageInstructions(string? errorMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {errorMessage}");
            Console.WriteLine();
            Console.ResetColor();
        }

        Console.ResetColor();
        Console.WriteLine("Usage:");
        Console.ResetColor();
        Console.WriteLine("Required parameters:");
        Console.ResetColor();
        Console.WriteLine("  expedite <source>                  Positional mode (preferred)");
        Console.WriteLine("  auth: use either --connection-string or --namespace");
        Console.WriteLine("  select: use either --message-id <id> or --all");
        Console.WriteLine("  source path: <queue> only (no subqueue, no subscription)");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Optional parameters:");
        Console.ResetColor();
        Console.WriteLine("  --source <source-path>             Alias for positional source");
        Console.WriteLine($"  --peek-batch-size <n>              Messages peeked per scan batch (default: {Constants.DefaultPeekBatchSize}, max: 500)");
        Console.WriteLine("  --prefetch-count <n>               Receiver prefetch count (default: 0)");
        Console.WriteLine("  --continue-on-failure              Continue when an individual expedite operation fails");
        Console.WriteLine($"  --client-id-prefix <value>         Identifier for Service Bus clients (default: {Constants.DefaultIdentifier})");
        Console.WriteLine("  --output <format>                  Completion report format: tsv (default) or json");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Help:");
        Console.ResetColor();
        Console.WriteLine("  -h, --help                         Show this help message");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Examples:");
        Console.ResetColor();
        Console.WriteLine("  asbctl expedite orders --message-id orders-1 --namespace mynamespace");
        Console.WriteLine("  asbctl expedite orders --all --connection-string \"Endpoint=sb://...\"");
        Console.ResetColor();
    }

}