using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class OutputWriterTests
{
    [Fact]
    public void PrintJsonEnvelope_UsesCamelCaseContract()
    {
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            OutputWriter.PrintJsonEnvelope(
                command: "move",
                resource: new { source = "orders", target = "orders-replay" },
                startedAt: new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc),
                completedAt: new DateTime(2026, 6, 22, 10, 0, 5, DateTimeKind.Utc),
                status: "completed",
                summary: new { processed = 10, moved = 10, failed = 0 },
                details: new { averageRate = 2.0 });
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("\"command\": {", output);
        Assert.Contains("\"name\": \"move\"", output);
        Assert.Contains("\"resource\":", output);
        Assert.Contains("\"startedAt\":", output);
        Assert.Contains("\"completedAt\":", output);
        Assert.Contains("\"duration\":", output);
        Assert.Contains("\"durationSeconds\": 5", output);
        Assert.Contains("\"status\": \"completed\"", output);
        Assert.Contains("\"summary\":", output);
        Assert.Contains("\"details\":", output);
    }
}
