using Cjoergensen.Azure.ServiceBus.Tools.Asbctl.Commands;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class ShellCompletionTests
{
    private static readonly string[] CommandNames = ["move", "purge", "inspect", "schedule", "expedite"];

    [Fact]
    public void GetCompletions_ForFirstToken_SuggestsCommandPrefixMatches()
    {
        var completions = ShellCompletion.GetCompletions(
            currentWordIndex: 1,
            words: ["pur"],
            commandNames: CommandNames);

        Assert.Equal(["purge"], completions);
    }

    [Fact]
    public void GetCompletions_ForCommandOption_SuggestsMatchingOption()
    {
        var completions = ShellCompletion.GetCompletions(
            currentWordIndex: 2,
            words: ["purge", "--he"],
            commandNames: CommandNames);

        Assert.Equal(["--help"], completions);
    }

    [Fact]
    public void GetCompletions_ForCommandSpecificOptions_SuggestsKnownOptions()
    {
        var completions = ShellCompletion.GetCompletions(
            currentWordIndex: 2,
            words: ["purge", "--na"],
            commandNames: CommandNames);

        Assert.Equal(["--namespace"], completions);
    }

    [Fact]
    public void GetCompletions_WhenPreviousOptionRequiresValue_DoesNotSuggestOptions()
    {
        var completions = ShellCompletion.GetCompletions(
            currentWordIndex: 2,
            words: ["purge", "--namespace"],
            commandNames: CommandNames);

        Assert.Empty(completions);
    }
}
