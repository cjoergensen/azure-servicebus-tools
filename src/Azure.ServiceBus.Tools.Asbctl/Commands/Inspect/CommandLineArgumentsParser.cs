using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;

public static class CommandLineArgumentsParser
{
    public class ArgumentParsingResult
    {
        public bool ShowHelp { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? ServiceBusNamespace { get; init; }
        public InspectEndpoint? Endpoint { get; init; }
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
        string? entityPath = null;
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

                if (SharedCommandOptions.TryParseOutputOption(reader, arg, ref outputFormat))
                {
                    continue;
                }

                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                        return new ArgumentParsingResult { ShowHelp = true };
                    case "--entity":
                        entityPath = reader.ReadRequiredValue(arg);
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            ApplyPositionalArguments(positional, ref entityPath);
            var endpoint = ParseEntityPath(
                entityPath,
                "Entity path is required: use inspect <entity-path> or --entity.");

            CommandLineValidation.ValidateAuthentication(connectionString, serviceBusNamespace);

            return new ArgumentParsingResult
            {
                ShowHelp = false,
                ServiceBusConnectionString = connectionString,
                ServiceBusNamespace = serviceBusNamespace,
                Endpoint = endpoint,
                OutputFormat = outputFormat
            };
        }
        catch (ArgumentException ex)
        {
            return new ArgumentParsingResult { ShowHelp = true, ErrorMessage = ex.Message };
        }
    }

    private static void ApplyPositionalArguments(List<string> positional, ref string? entityPath)
    {
        if (positional.Count == 0)
        {
            return;
        }

        var tokens = CommandLineValidation.ResolveCommandOperands(positional, "inspect");

        if (tokens.Count == 0)
        {
            return;
        }

        CommandLineValidation.EnsureExpectedOperandCount(
            tokens,
            expectedCount: 1,
            usageError: "Usage requires exactly one entity path operand: inspect <entity-path>.");
        CommandLineValidation.EnsureNotMixedWithOptionValue(
            !string.IsNullOrWhiteSpace(entityPath),
            "Specify entity with either positional path or --entity, not both.");

        entityPath = tokens[0];
    }

    private static InspectEndpoint ParseEntityPath(string? path, string missingPathMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(missingPathMessage);
        }

        var trimmedPath = path.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            throw new ArgumentException("Entity path cannot be empty.");
        }

        if (trimmedPath.Contains(':'))
        {
            throw new ArgumentException($"Invalid Azure Service Bus path '{path}'. Use native entity paths only.");
        }

        var (basePath, subQueue) = ParseSubQueueSuffix(trimmedPath);
        var segments = basePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 1)
        {
            return new InspectEndpoint(InspectEntityKind.Entity, segments[0], null, subQueue);
        }

        if (segments.Length == 3 && segments[1].Equals("Subscriptions", StringComparison.OrdinalIgnoreCase))
        {
            if (subQueue == SourceSubQueue.TransferDeadLetter)
            {
                throw new ArgumentException(
                    $"Invalid inspect path '{path}': '/$Transfer/$DeadLetterQueue' is only supported for queue paths.");
            }

            return new InspectEndpoint(InspectEntityKind.Subscription, segments[0], segments[2], subQueue);
        }

        throw new ArgumentException(
            $"Invalid Azure Service Bus path '{path}'. Use '<queue-or-topic>' or '<topic>/Subscriptions/<subscription>' with optional subqueue suffix.");
    }

    private static (string basePath, SourceSubQueue subQueue) ParseSubQueueSuffix(string path)
    {
        const string deadLetterSuffix = "/$DeadLetterQueue";
        const string transferDeadLetterSuffix = "/$Transfer/$DeadLetterQueue";

        if (path.EndsWith(transferDeadLetterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return (path[..^transferDeadLetterSuffix.Length], SourceSubQueue.TransferDeadLetter);
        }

        if (path.EndsWith(deadLetterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return (path[..^deadLetterSuffix.Length], SourceSubQueue.DeadLetter);
        }

        return (path, SourceSubQueue.None);
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
        Console.WriteLine("  inspect <entity-path>             Positional mode (preferred)");
        Console.WriteLine("  auth: use either --connection-string or --namespace");
        Console.WriteLine("  -c, --connection-string <value>   Azure Service Bus connection string");
        Console.WriteLine("  --namespace <name-or-host>        Service Bus namespace for Azure credential auth");
        Console.WriteLine("  entity path: <queue-or-topic> or <topic>/Subscriptions/<subscription>, with optional subqueue suffix");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Optional parameters:");
        Console.ResetColor();
        Console.WriteLine("  --entity <entity-path>            Alias for positional entity");
        Console.WriteLine("  --output <format>                 Output format: tsv (default) or json");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Help:");
        Console.ResetColor();
        Console.WriteLine("  -h, --help                        Show this help message");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Examples:");
        Console.ResetColor();
        Console.WriteLine("  asbctl inspect orders --namespace mynamespace.servicebus.windows.net");
        Console.WriteLine("  asbctl inspect orders-events --connection-string \"Endpoint=sb://...\"");
        Console.WriteLine("  asbctl inspect orders-events/Subscriptions/replay-sub --namespace mynamespace");
        Console.WriteLine("  asbctl inspect orders-events/Subscriptions/replay-sub/$DeadLetterQueue --namespace mynamespace");
        Console.ResetColor();
    }

}