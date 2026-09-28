using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityLister;

public static class CommandLineArgumentsParser
{
    public sealed class ArgumentParsingResult
    {
        public bool ShowHelp { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ServiceBusConnectionString { get; init; }
        public string? ServiceBusNamespace { get; init; }
        public ListEntityKind? EntityKind { get; init; }
        public string? TopicName { get; init; }
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
        ListEntityKind? entityKind = null;
        string? topicName = null;
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
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            ApplyPositionalArguments(positional, ref entityKind, ref topicName);

            if (entityKind is null)
            {
                throw new ArgumentException("Subcommand is required: use 'list queues', 'list topics', or 'list subscriptions <topic>'.");
            }

            CommandLineValidation.ValidateAuthentication(connectionString, serviceBusNamespace);

            return new ArgumentParsingResult
            {
                ShowHelp = false,
                ServiceBusConnectionString = connectionString,
                ServiceBusNamespace = serviceBusNamespace,
                EntityKind = entityKind,
                TopicName = topicName,
                OutputFormat = outputFormat
            };
        }
        catch (ArgumentException ex)
        {
            return new ArgumentParsingResult { ShowHelp = true, ErrorMessage = ex.Message };
        }
    }

    private static void ApplyPositionalArguments(List<string> positional, ref ListEntityKind? entityKind, ref string? topicName)
    {
        if (positional.Count == 0)
        {
            return;
        }

        var tokens = CommandLineValidation.ResolveCommandOperands(positional, "list");

        if (tokens.Count == 0)
        {
            return;
        }

        entityKind = tokens[0].ToLowerInvariant() switch
        {
            "queues" => ListEntityKind.Queues,
            "topics" => ListEntityKind.Topics,
            "subscriptions" => ListEntityKind.Subscriptions,
            _ => throw new ArgumentException($"Unknown subcommand '{tokens[0]}'. Use 'queues', 'topics', or 'subscriptions'.")
        };

        if (entityKind == ListEntityKind.Subscriptions)
        {
            if (tokens.Count < 2)
            {
                throw new ArgumentException("'list subscriptions' requires a topic name: list subscriptions <topic>.");
            }
            if (tokens.Count > 2)
            {
                throw new ArgumentException("'list subscriptions' accepts exactly one topic name argument.");
            }
            topicName = tokens[1];
        }
        else if (tokens.Count > 1)
        {
            throw new ArgumentException($"'list {tokens[0]}' does not accept additional arguments.");
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
        Console.WriteLine("Subcommands:");
        Console.ResetColor();
        Console.WriteLine("  list queues                       List all queues");
        Console.WriteLine("  list topics                       List all topics");
        Console.WriteLine("  list subscriptions <topic>        List all subscriptions for a topic");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Required parameters:");
        Console.ResetColor();
        Console.WriteLine("  auth: use either --connection-string or --namespace");
        Console.WriteLine("  -c, --connection-string <value>   Azure Service Bus connection string");
        Console.WriteLine("  --namespace <name-or-host>        Service Bus namespace for Azure credential auth");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Optional parameters:");
        Console.ResetColor();
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
        Console.WriteLine("  asbctl list queues --namespace mynamespace");
        Console.WriteLine("  asbctl list topics --connection-string \"Endpoint=sb://...\"");
        Console.WriteLine("  asbctl list subscriptions orders-events --namespace mynamespace");
        Console.WriteLine("  asbctl list queues --namespace mynamespace --output json");
        Console.WriteLine("  asbctl list queues --namespace mynamespace --output tsv");
        Console.ResetColor();
    }

}