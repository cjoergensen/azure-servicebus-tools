using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Purger;

public static class CommandLineArgumentsParser
{
    public class ArgumentParsingResult
    {
        public bool ShowHelp { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? ServiceBusNamespace { get; init; }
        public SourceEndpoint? Source { get; init; }
        public PurgerBehavior? Behavior { get; init; }
        public int MaxConcurrency { get; init; }
        public int MaxMessagesPerBatch { get; init; }
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
        var continueOnError = false;
        int? maxMessages = null;
        var maxConcurrency = SharedConstants.DefaultMaxConcurrency;
        var maxMessagesPerBatch = SharedConstants.DefaultMaxMessagesPerBatch;
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

                if (SharedCommandOptions.TryParseWorkerOptions(reader, arg, ref maxConcurrency, ref maxMessagesPerBatch, ref prefetchCount))
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
                    case "--continue-on-failure":
                        continueOnError = true;
                        break;
                    case "--limit":
                        maxMessages = CommandLineParsing.ParsePositiveInt(reader.ReadRequiredValue(arg), "max messages");
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            ApplyPositionalArguments(positional, ref sourcePath);
            var source = CommandLineParsing.ParseSourcePath(
                sourcePath,
                "Source path is required: use purge <source-path> or --source.");
            var behavior = new PurgerBehavior(continueOnError, maxMessages);

            ValidateArguments(connectionString, serviceBusNamespace, source, behavior, maxConcurrency, maxMessagesPerBatch, prefetchCount);

            return new ArgumentParsingResult
            {
                ShowHelp = false,
                ServiceBusConnectionString = connectionString,
                ServiceBusNamespace = serviceBusNamespace,
                Source = source,
                Behavior = behavior,
                MaxConcurrency = maxConcurrency,
                MaxMessagesPerBatch = maxMessagesPerBatch,
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

        var tokens = CommandLineValidation.ResolveCommandOperands(positional, "purge");

        if (tokens.Count == 0)
        {
            return;
        }

        CommandLineValidation.EnsureExpectedOperandCount(
            tokens,
            expectedCount: 1,
            usageError: "Usage requires exactly one source path operand: purge <source-path>.");
        CommandLineValidation.EnsureNotMixedWithOptionValue(
            !string.IsNullOrWhiteSpace(sourcePath),
            "Specify source with either positional path or --source, not both.");

        sourcePath = tokens[0];
    }

    private static void ValidateArguments(
        string? connectionString,
        string? serviceBusNamespace,
        SourceEndpoint source,
        PurgerBehavior behavior,
        int maxConcurrency,
        int maxMessagesPerBatch,
        int prefetchCount)
    {
        CommandLineValidation.ValidateAuthentication(connectionString, serviceBusNamespace);
        CommandLineValidation.ValidateSourcePathCompatibility(source);
        CommandLineValidation.ValidateWorkerSettings(maxConcurrency, maxMessagesPerBatch, prefetchCount, behavior.MaxMessages);
    }

    public static void PrintUsageInstructions(string? errorMessage = null)
    {
        if (!string.IsNullOrEmpty(errorMessage))
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
        Console.WriteLine("  purge <source>                   Positional mode (preferred)");
        Console.WriteLine("  auth: use either --connection-string or --namespace");
        Console.WriteLine("  -c, --connection-string <value>  Azure Service Bus connection string");
        Console.WriteLine("  --namespace <name-or-host>       Service Bus namespace for Azure credential auth");
        Console.WriteLine("  source path: <queue> or <topic>/Subscriptions/<subscription>");
        Console.WriteLine("  subqueue suffixes on source: /$DeadLetterQueue or /$Transfer/$DeadLetterQueue");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Optional parameters:");
        Console.ResetColor();
        Console.WriteLine("  --source <source-path>           Alias for positional source");
        Console.WriteLine($"  --workers <n>                    Number of parallel workers (default: {SharedConstants.DefaultMaxConcurrency}, max: {SharedConstants.MaxAllowedConcurrency})");
        Console.WriteLine($"  --receive-batch-size <n>         Messages received per worker iteration (default: {SharedConstants.DefaultMaxMessagesPerBatch}, max: {SharedConstants.MaxAllowedMessagesPerBatch})");
        Console.WriteLine("  --prefetch-count <n>             Receiver prefetch count (default: 0)");
        Console.WriteLine("  --continue-on-failure            Keep purging messages after individual failures");
        Console.WriteLine("  --limit <count>                  Stop after this many processed results");
        Console.WriteLine($"  --client-id-prefix <value>       Identifier prefix for Service Bus clients. Worker suffixes are appended automatically. (default: {Constants.DefaultIdentifier})");
        Console.WriteLine("  --output <format>                Completion report format: tsv (default) or json");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Help:");
        Console.ResetColor();
        Console.WriteLine("  -h, --help                       Show this help message");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Examples:");
        Console.ResetColor();
        Console.WriteLine("  asbctl purge orders --connection-string \"Endpoint=sb://...\" --workers 16 --receive-batch-size 50 --prefetch-count 200");
        Console.WriteLine("  asbctl purge legacy/Subscriptions/orders-sub/$DeadLetterQueue --namespace mynamespace");
        Console.WriteLine("  asbctl purge orders/$DeadLetterQueue --namespace mynamespace.servicebus.windows.net --limit 100");
        Console.ResetColor();
    }

}