using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl.Commands;

public static class AsbctlUsage
{
    public static void PrintUsageInstructions(IEnumerable<IAsbCommand>? commands = null, string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {error}");
            Console.WriteLine();
            Console.ResetColor();
        }

        Console.ResetColor();
        Console.WriteLine("asbctl - Azure Service Bus control CLI");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Usage:");
        Console.ResetColor();
        Console.WriteLine("  asbctl <command> [options]");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Commands:");
        Console.ResetColor();
        if (commands is null)
        {
            Console.WriteLine("  move       Move messages between entities");
            Console.WriteLine("  purge      Purge messages from a source");
            Console.WriteLine("  inspect    Read queue/topic/subscription properties");
            Console.WriteLine("  list       List queues, topics, or subscriptions with key properties");
            Console.WriteLine("  schedule   Schedule queue messages for later processing");
            Console.WriteLine("  expedite   Process scheduled queue messages immediately");
            Console.WriteLine("  completion Generate shell completion script");
            Console.WriteLine("  help       Show asbctl command usage");
        }
        else
        {
            foreach (var command in commands.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {command.Name,-10} {command.Description}");
            }
            Console.WriteLine("  completion Generate shell completion script");
            Console.WriteLine("  help       Show asbctl command usage");
        }
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Help:");
        Console.ResetColor();
        Console.WriteLine("  asbctl --help");
        Console.WriteLine("  asbctl <command> --help");
        Console.WriteLine("  source <(asbctl completion bash)");
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine("Examples:");
        Console.ResetColor();
        Console.WriteLine("  asbctl move orders orders-replay --namespace mynamespace");
        Console.WriteLine("  asbctl purge orders/$DeadLetterQueue --connection-string \"Endpoint=sb://...\"");
        Console.WriteLine("  asbctl inspect orders-events/Subscriptions/replay-sub --namespace mynamespace");
        Console.WriteLine("  asbctl schedule orders --all --for 00:30:00 --namespace mynamespace");
        Console.WriteLine("  asbctl expedite orders --all --namespace mynamespace");
        Console.ResetColor();
    }
}
