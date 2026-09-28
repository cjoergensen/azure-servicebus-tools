using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.MessageMover;

public static class CommandLineArgumentsParser
{
    public class ArgumentParsingResult
    {
        public bool ShowHelp { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? ServiceBusNamespace { get; init; }
        public SourceEndpoint? Source { get; init; }
        public TargetEndpoint? Target { get; init; }
        public MessageMoverBehavior? Behavior { get; init; }
        public int MaxConcurrency { get; init; }
        public int MaxMessagesPerBatch { get; init; }
        public int PrefetchCount { get; init; }
        public int MaxIdleMinutes { get; init; }
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
        string? fromPath = null;
        string? toPath = null;
        SettlementMode? onSuccess = null;
        SettlementMode? onFailure = null;
        var continueOnError = false;
        var dryRun = false;
        int? maxMessages = null;
        var maxConcurrency = SharedConstants.DefaultMaxConcurrency;
        var maxMessagesPerBatch = SharedConstants.DefaultMaxMessagesPerBatch;
        var prefetchCount = 0;
        var maxIdleMinutes = SharedConstants.DefaultMaxIdleMinutes;
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
                        fromPath = reader.ReadRequiredValue(arg);
                        break;
                    case "--target":
                        toPath = reader.ReadRequiredValue(arg);
                        break;
                    case "--success-settlement":
                        onSuccess = ParseSettlementMode(reader.ReadRequiredValue(arg));
                        break;
                    case "--failure-settlement":
                        onFailure = ParseSettlementMode(reader.ReadRequiredValue(arg));
                        break;
                    case "--continue-on-failure":
                        continueOnError = true;
                        break;
                    case "--dry-run":
                        dryRun = true;
                        break;
                    case "--limit":
                        maxMessages = CommandLineParsing.ParsePositiveInt(reader.ReadRequiredValue(arg), "max messages");
                        break;
                    case "--max-idle-minutes":
                        maxIdleMinutes = CommandLineParsing.ParsePositiveInt(reader.ReadRequiredValue(arg), "max idle minutes");
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            ApplyPositionalArguments(positional, ref fromPath, ref toPath);

            var source = CommandLineParsing.ParseSourcePath(
                fromPath,
                "Source path is required: use move <source-path> <target-path> or --source.");
            var target = ParseTargetPath(toPath);
            var behavior = BuildBehavior(onSuccess, onFailure, continueOnError, dryRun, maxMessages);

            ValidateArguments(connectionString, serviceBusNamespace, source, target, behavior, maxConcurrency, maxMessagesPerBatch, prefetchCount);

            return new ArgumentParsingResult
            {
                ShowHelp = false,
                ServiceBusConnectionString = connectionString,
                ServiceBusNamespace = serviceBusNamespace,
                Source = source,
                Target = target,
                Behavior = behavior,
                MaxConcurrency = maxConcurrency,
                MaxMessagesPerBatch = maxMessagesPerBatch,
                PrefetchCount = prefetchCount,
                MaxIdleMinutes = maxIdleMinutes,
                Identifier = string.IsNullOrWhiteSpace(identifier) ? Constants.DefaultIdentifier : identifier,
                OutputFormat = outputFormat
            };
        }
        catch (ArgumentException ex)
        {
            return new ArgumentParsingResult { ShowHelp = true, ErrorMessage = ex.Message };
        }
    }

    private static void ApplyPositionalArguments(List<string> positional, ref string? fromPath, ref string? toPath)
    {
        if (positional.Count == 0)
        {
            return;
        }

        var tokens = CommandLineValidation.ResolveCommandOperands(positional, "move");

        if (tokens.Count == 0)
        {
            return;
        }

        CommandLineValidation.EnsureExpectedOperandCount(
            tokens,
            expectedCount: 2,
            usageError: "Usage requires exactly two path operands: move <source-path> <target-path>.");
        CommandLineValidation.EnsureNotMixedWithOptionValue(
            !string.IsNullOrWhiteSpace(fromPath) || !string.IsNullOrWhiteSpace(toPath),
            "Specify source/target with either positional paths or --source/--target, not both.");

        fromPath = tokens[0];
        toPath = tokens[1];
    }

    private static TargetEndpoint ParseTargetPath(string? path)
    {
        return new TargetEndpoint(CommandLineParsing.ParseTargetEntityPath(
            path,
            "Target path is required: use move <source-path> <target-path> or --target."));
    }

    private static MessageMoverBehavior BuildBehavior(
        SettlementMode? onSuccess,
        SettlementMode? onFailure,
        bool continueOnError,
        bool dryRun,
        int? maxMessages)
    {
        var effectiveOnSuccess = onSuccess ?? (dryRun ? SettlementMode.None : SettlementMode.Complete);
        var effectiveOnFailure = onFailure ?? SettlementMode.Abandon;

        return new MessageMoverBehavior(
            effectiveOnSuccess,
            effectiveOnFailure,
            continueOnError,
            dryRun,
            maxMessages);
    }

    private static SettlementMode ParseSettlementMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "complete" => SettlementMode.Complete,
            "abandon" => SettlementMode.Abandon,
            "deadletter" => SettlementMode.DeadLetter,
            "none" => SettlementMode.None,
            _ => throw new ArgumentException("Invalid settlement mode: use complete, abandon, deadletter, or none.")
        };
    }

    private static void ValidateArguments(
        string? connectionString,
        string? serviceBusNamespace,
        SourceEndpoint source,
        TargetEndpoint target,
        MessageMoverBehavior behavior,
        int maxConcurrency,
        int maxMessagesPerBatch,
        int prefetchCount)
    {
        CommandLineValidation.ValidateAuthentication(connectionString, serviceBusNamespace);
        CommandLineValidation.ValidateSourcePathCompatibility(source);

        if (string.IsNullOrWhiteSpace(target.EntityName))
        {
            throw new ArgumentException("Target path is required.");
        }
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
        Console.WriteLine("  move <source> <target>       Positional mode (preferred)");
        Console.WriteLine("  auth: use either --connection-string or --namespace");
        Console.WriteLine("  -c, --connection-string <value>    Azure Service Bus connection string");
        Console.WriteLine("  --namespace <name-or-host>         Service Bus namespace for Azure credential auth");
        Console.WriteLine("  source path: <queue> or <topic>/Subscriptions/<subscription>");
        Console.WriteLine("  target path: <queue-or-topic>");
        Console.WriteLine("  subqueue suffixes on source: /$DeadLetterQueue or /$Transfer/$DeadLetterQueue");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Optional parameters:");
        Console.ResetColor();
        Console.WriteLine("  --source <source-path>       Alias for positional source");
        Console.WriteLine("  --target <target-path>       Alias for positional target");
        Console.WriteLine($"  --workers <n>                Number of parallel workers (default: {SharedConstants.DefaultMaxConcurrency}, max: {SharedConstants.MaxAllowedConcurrency})");
        Console.WriteLine($"  --receive-batch-size <n>     Messages received per worker iteration (default: {SharedConstants.DefaultMaxMessagesPerBatch}, max: {SharedConstants.MaxAllowedMessagesPerBatch})");
        Console.WriteLine("  --prefetch-count <n>         Receiver prefetch count (default: 0)");
        Console.WriteLine($"  --max-idle-minutes <n>       Keep polling while source count is non-zero until idle timeout (default: {SharedConstants.DefaultMaxIdleMinutes})");
        Console.WriteLine("  --success-settlement <complete|abandon|deadletter|none>");
        Console.WriteLine("  --failure-settlement <complete|abandon|deadletter|none>");
        Console.WriteLine("  --continue-on-failure        Keep moving messages after individual failures");
        Console.WriteLine("  --dry-run                    Read only, do not send to target");
        Console.WriteLine("  --limit <count>              Stop after this many processed results");
        Console.WriteLine($"  --client-id-prefix <value>   Identifier prefix for Service Bus clients. Worker suffixes are appended automatically. (default: {Constants.DefaultIdentifier})");
        Console.WriteLine("  --output <format>            Completion report format: tsv (default) or json");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Help:");
        Console.ResetColor();
        Console.WriteLine("  -h, --help                   Show this help message");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Examples:");
        Console.ResetColor();
        Console.WriteLine("  asbctl move orders orders-replay --connection-string \"Endpoint=sb://...\" --workers 16 --receive-batch-size 50 --prefetch-count 200");
        Console.WriteLine("  asbctl move legacy/Subscriptions/orders-sub/$DeadLetterQueue orders --namespace mynamespace");
        Console.WriteLine("  asbctl move orders/$DeadLetterQueue orders --namespace mynamespace.servicebus.windows.net --dry-run --limit 100");
        Console.ResetColor();
    }

}