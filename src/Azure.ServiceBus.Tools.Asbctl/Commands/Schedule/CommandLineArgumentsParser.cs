using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Schedule;

public static class CommandLineArgumentsParser
{
    public class ArgumentParsingResult
    {
        public bool ShowHelp { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? ServiceBusNamespace { get; init; }
        public SourceEndpoint? Source { get; init; }
        public ScheduleBehavior? Behavior { get; init; }
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
        string? messageId = null;
        string? forDurationValue = null;
        string? untilValue = null;
        var scheduleAll = false;
        var continueOnError = false;
        var ignoredMaxConcurrency = SharedConstants.DefaultMaxConcurrency;
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

                if (SharedCommandOptions.TryParseWorkerOptions(reader, arg, ref ignoredMaxConcurrency, ref maxMessagesPerBatch, ref prefetchCount, allowWorkers: false))
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
                        scheduleAll = true;
                        break;
                    case "--for":
                        forDurationValue = reader.ReadRequiredValue(arg);
                        break;
                    case "--until":
                        untilValue = reader.ReadRequiredValue(arg);
                        break;
                    case "--continue-on-failure":
                        continueOnError = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            ApplyPositionalArguments(positional, ref sourcePath);
            var source = CommandLineParsing.ParseSourcePath(
                sourcePath,
                "Source path is required: use schedule <source-path> or --source.");

            var scheduleAtUtc = ParseScheduleAtUtc(forDurationValue, untilValue);
            var behavior = BuildBehavior(scheduleAll, messageId, scheduleAtUtc, continueOnError);
            ValidateArguments(connectionString, serviceBusNamespace, source, behavior, maxMessagesPerBatch, prefetchCount);

            return new ArgumentParsingResult
            {
                ShowHelp = false,
                ServiceBusConnectionString = connectionString,
                ServiceBusNamespace = serviceBusNamespace,
                Source = source,
                Behavior = behavior,
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

        var tokens = CommandLineValidation.ResolveCommandOperands(positional, "schedule");

        if (tokens.Count == 0)
        {
            return;
        }

        CommandLineValidation.EnsureExpectedOperandCount(
            tokens,
            expectedCount: 1,
            usageError: "Usage requires exactly one source path operand: schedule <source-path>.");
        CommandLineValidation.EnsureNotMixedWithOptionValue(
            !string.IsNullOrWhiteSpace(sourcePath),
            "Specify source with either positional path or --source, not both.");

        sourcePath = tokens[0];
    }

    private static DateTimeOffset ParseScheduleAtUtc(string? forDurationValue, string? untilValue)
    {
        var hasFor = !string.IsNullOrWhiteSpace(forDurationValue);
        var hasUntil = !string.IsNullOrWhiteSpace(untilValue);

        if (hasFor == hasUntil)
        {
            throw new ArgumentException("Specify exactly one of --for <timespan> or --until <utc-datetime>.");
        }

        if (hasFor)
        {
            if (!TimeSpan.TryParse(forDurationValue, out var duration))
            {
                throw new ArgumentException("Invalid --for value: use a timespan like 00:30:00.");
            }

            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentException("Invalid --for value: duration must be greater than zero.");
            }

            return DateTimeOffset.UtcNow.Add(duration);
        }

        if (!DateTimeOffset.TryParse(untilValue, out var until))
        {
            throw new ArgumentException("Invalid --until value: use an ISO-8601 UTC datetime like 2026-06-21T20:00:00Z.");
        }

        var untilUtc = until.ToUniversalTime();
        if (untilUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Invalid --until value: datetime must be in the future.");
        }

        return untilUtc;
    }

    private static ScheduleBehavior BuildBehavior(
        bool scheduleAll,
        string? messageId,
        DateTimeOffset scheduleAtUtc,
        bool continueOnError)
    {
        if (scheduleAll == !string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Specify exactly one of --all or --message-id <id>.");
        }

        return new ScheduleBehavior(scheduleAll, messageId, scheduleAtUtc, continueOnError);
    }

    private static void ValidateArguments(
        string? connectionString,
        string? serviceBusNamespace,
        SourceEndpoint source,
        ScheduleBehavior behavior,
        int maxMessagesPerBatch,
        int prefetchCount)
    {
        CommandLineValidation.ValidateAuthentication(connectionString, serviceBusNamespace);
        CommandLineValidation.ValidateSourcePathCompatibility(source);

        if (source.Kind != SourceEntityKind.Queue)
        {
            throw new ArgumentException("Schedule currently supports queue sources only.");
        }

        if (behavior.ScheduleAll && maxMessagesPerBatch > SharedConstants.MaxAllowedMessagesPerBatch)
        {
            throw new ArgumentException($"Max messages per batch cannot exceed {SharedConstants.MaxAllowedMessagesPerBatch}.");
        }

        if (maxMessagesPerBatch <= 0)
        {
            throw new ArgumentException("Max messages per batch must be a positive number.");
        }

        if (maxMessagesPerBatch > SharedConstants.MaxAllowedMessagesPerBatch)
        {
            throw new ArgumentException($"Max messages per batch cannot exceed {SharedConstants.MaxAllowedMessagesPerBatch}.");
        }

        if (prefetchCount < 0)
        {
            throw new ArgumentException("Prefetch count must be zero or greater.");
        }
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
        Console.WriteLine("  schedule <source>                  Positional mode (preferred)");
        Console.WriteLine("  auth: use either --connection-string or --namespace");
        Console.WriteLine("  select: use either --message-id <id> or --all");
        Console.WriteLine("  schedule: use exactly one of --for <timespan> or --until <utc-datetime>");
        Console.WriteLine("  source path: <queue> with optional subqueue suffix");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Optional parameters:");
        Console.ResetColor();
        Console.WriteLine("  --source <source-path>             Alias for positional source");
        Console.WriteLine($"  --receive-batch-size <n>           Messages received per iteration (default: {SharedConstants.DefaultMaxMessagesPerBatch}, max: {SharedConstants.MaxAllowedMessagesPerBatch})");
        Console.WriteLine("  --prefetch-count <n>               Receiver prefetch count (default: 0)");
        Console.WriteLine("  --continue-on-failure              Continue when an individual schedule operation fails");
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
        Console.WriteLine("  asbctl schedule orders --message-id orders-1 --for 00:30:00 --namespace mynamespace");
        Console.WriteLine("  asbctl schedule orders --all --until 2026-06-22T08:00:00Z --connection-string \"Endpoint=sb://...\"");
        Console.ResetColor();
    }

}