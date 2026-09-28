using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl.Commands;

public static class ShellCompletion
{
    private static readonly string[] GlobalHelpOptions = ["-h", "--help"];
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static readonly Dictionary<string, CommandOptionCatalog> CommandOptions = new(Comparer)
    {
        ["move"] = new(
            [
                "-h", "--help",
                "-c", "--connection-string", "--namespace",
                "--workers", "--receive-batch-size", "--prefetch-count", "--client-id-prefix",
                "--source", "--target",
                "--success-settlement", "--failure-settlement",
                "--continue-on-failure", "--dry-run",
                "--limit", "--max-idle-minutes", "--output"
            ],
            
            [
                "-c", "--connection-string", "--namespace",
                "--workers", "--receive-batch-size", "--prefetch-count", "--client-id-prefix",
                "--source", "--target",
                "--success-settlement", "--failure-settlement",
                "--limit", "--max-idle-minutes", "--output"
            ]),
        ["purge"] = new(
            [
                "-h", "--help",
                "-c", "--connection-string", "--namespace",
                "--workers", "--receive-batch-size", "--prefetch-count", "--client-id-prefix",
                "--source", "--continue-on-failure", "--limit", "--output"
            ],
            
            [
                "-c", "--connection-string", "--namespace",
                "--workers", "--receive-batch-size", "--prefetch-count", "--client-id-prefix",
                "--source", "--limit", "--output"
            ]),
        ["inspect"] = new(
            [
                "-h", "--help",
                "-c", "--connection-string", "--namespace",
                "--entity", "--output"
            ],
            
            [
                "-c", "--connection-string", "--namespace", "--entity", "--output"
            ]),
        ["list"] = new(
            [
                "-h", "--help",
                "-c", "--connection-string", "--namespace",
                "--output"
            ],
            
            [
                "-c", "--connection-string", "--namespace", "--output"
            ]),
        ["schedule"] = new(
            [
                "-h", "--help",
                "-c", "--connection-string", "--namespace",
                "--receive-batch-size", "--prefetch-count", "--client-id-prefix",
                "--source", "--message-id", "--all", "--for", "--until", "--continue-on-failure", "--output"
            ],
            
            [
                "-c", "--connection-string", "--namespace",
                "--receive-batch-size", "--prefetch-count", "--client-id-prefix",
                "--source", "--message-id", "--for", "--until", "--output"
            ]),
        ["expedite"] = new(
            [
                "-h", "--help",
                "-c", "--connection-string", "--namespace",
                "--prefetch-count", "--client-id-prefix",
                "--source", "--message-id", "--all", "--continue-on-failure", "--peek-batch-size", "--output"
            ],
            
            [
                "-c", "--connection-string", "--namespace",
                "--prefetch-count", "--client-id-prefix",
                "--source", "--message-id", "--peek-batch-size", "--output"
            ]),
        ["help"] = new(["-h", "--help"], []),
        ["completion"] = new(["-h", "--help", "bash", "zsh"], []),
    };

    public static bool TryHandle(string[] args, IEnumerable<IAsbCommand> commands)
    {
        if (args.Length == 0)
        {
            return false;
        }

        if (args[0].Equals("completion", StringComparison.OrdinalIgnoreCase))
        {
            var shell = args.Length > 1 ? args[1] : "bash";
            if (shell.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                shell.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintCompletionUsage();
                return true;
            }

            switch (shell.ToLowerInvariant())
            {
                case "bash":
                    Console.WriteLine(BashScript);
                    return true;
                case "zsh":
                    Console.WriteLine(ZshScript);
                    return true;
                default:
                    Console.Error.WriteLine($"Unsupported shell '{shell}'. Use 'bash' or 'zsh'.");
                    Environment.ExitCode = 1;
                    return true;
            }
        }

        if (!args[0].Equals("__complete", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length < 2 || !int.TryParse(args[1], out var currentWordIndex))
        {
            Environment.ExitCode = 1;
            return true;
        }

        var knownCommandNames = commands.Select(command => command.Name).ToArray();
        var words = args.Skip(2).ToArray();
        foreach (var completion in GetCompletions(currentWordIndex, words, knownCommandNames))
        {
            Console.WriteLine(completion);
        }

        return true;
    }

    public static IReadOnlyList<string> GetCompletions(
        int currentWordIndex,
        IReadOnlyList<string> words,
        IReadOnlyList<string> commandNames)
    {
        var currentTokenIndex = Math.Max(currentWordIndex - 1, 0);
        var currentToken = currentTokenIndex < words.Count ? words[currentTokenIndex] : string.Empty;
        var knownCommandNames = commandNames
            .Concat(["help", "completion"])
            .Distinct(Comparer)
            .OrderBy(name => name, Comparer)
            .ToArray();

        if (currentTokenIndex == 0)
        {
            if (currentToken.StartsWith("-", StringComparison.Ordinal))
            {
                return FilterByPrefix(GlobalHelpOptions, currentToken);
            }

            return FilterByPrefix(knownCommandNames, currentToken);
        }

        var commandToken = words.Count > 0 ? words[0] : string.Empty;
        var commandName = ResolveCommand(commandToken, knownCommandNames);
        if (commandName is null || !CommandOptions.TryGetValue(commandName, out var catalog))
        {
            return Array.Empty<string>();
        }

        if (ShouldSuppressSuggestions(words, currentTokenIndex, catalog.OptionsRequiringValue))
        {
            return Array.Empty<string>();
        }

        if (currentToken.StartsWith("-", StringComparison.Ordinal))
        {
            return FilterByPrefix(catalog.Options, currentToken);
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> FilterByPrefix(IEnumerable<string> values, string prefix)
    {
        return values
            .Where(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(Comparer)
            .OrderBy(value => value, Comparer)
            .ToArray();
    }

    private static bool ShouldSuppressSuggestions(
        IReadOnlyList<string> words,
        int currentTokenIndex,
        HashSet<string> optionsRequiringValue)
    {
        if (words.Count == 0)
        {
            return false;
        }

        // The current token itself requires a value: user finished typing the option name,
        // so don't suggest more options — they need to type the value next.
        if (currentTokenIndex < words.Count && optionsRequiringValue.Contains(words[currentTokenIndex]))
        {
            return true;
        }

        var previousTokenIndex = Math.Min(currentTokenIndex - 1, words.Count - 1);
        if (previousTokenIndex < 0)
        {
            return false;
        }

        return optionsRequiringValue.Contains(words[previousTokenIndex]);
    }

    private static string? ResolveCommand(string token, IReadOnlyList<string> commandNames)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var exact = commandNames.FirstOrDefault(name => name.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var prefixMatches = commandNames
            .Where(name => name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return prefixMatches.Length == 1 ? prefixMatches[0] : null;
    }

    private static void PrintCompletionUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  asbctl completion <bash|zsh>");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  source <(asbctl completion bash)");
        Console.WriteLine("  source <(asbctl completion zsh)");
    }

    private sealed record CommandOptionCatalog(string[] Options, string[] OptionsRequiringValueTokens)
    {
        public HashSet<string> OptionsRequiringValue { get; } = new(OptionsRequiringValueTokens, Comparer);
    }

    private const string BashScript = """
_asbctl_completion() {
  local cword="${COMP_CWORD}"
  local words=("${COMP_WORDS[@]:1}")
  local output
  output="$(asbctl __complete "$cword" "${words[@]}" 2>/dev/null)" || return 0

  COMPREPLY=()
  while IFS= read -r line; do
    [[ -n "$line" ]] && COMPREPLY+=("$line")
  done <<< "$output"
}

complete -F _asbctl_completion asbctl
""";

    private const string ZshScript = """
autoload -U +X bashcompinit && bashcompinit

_asbctl_completion() {
  local cword="${COMP_CWORD}"
  local words=("${COMP_WORDS[@]:1}")
  local output
  output="$(asbctl __complete "$cword" "${words[@]}" 2>/dev/null)" || return 0

  COMPREPLY=()
  while IFS= read -r line; do
    [[ -n "$line" ]] && COMPREPLY+=("$line")
  done <<< "$output"
}

complete -F _asbctl_completion asbctl
""";
}
