using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class ConsoleProgressReporterTests
{
    [Fact]
    public void ResolveGoal_WithLimit_PrioritizesLimit()
    {
        var goal = ConsoleProgressReporter.ResolveGoal(25, 500);

        Assert.Equal(25, goal);
    }

    [Fact]
    public void ResolveGoal_WithSourceCountUnderIntMax_UsesSourceCount()
    {
        var goal = ConsoleProgressReporter.ResolveGoal(null, 123);

        Assert.Equal(123, goal);
    }

    [Fact]
    public void ResolveGoal_WithUnknownSourceCount_ReturnsNull()
    {
        var goal = ConsoleProgressReporter.ResolveGoal(null, null);

        Assert.Null(goal);
    }

    [Fact]
    public void Print_WritesUnifiedProgressLine()
    {
        var startTime = DateTime.Now.AddSeconds(-10);
        var reporter = new ConsoleProgressReporter(startTime, "moved", () => 20);
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            reporter.Print(8, 2);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("Processed 10", output);
        Assert.Contains("(moved 8, failed 2)", output);
        Assert.Contains("goal 20", output);
        Assert.Contains("remaining 10", output);
    }
}
