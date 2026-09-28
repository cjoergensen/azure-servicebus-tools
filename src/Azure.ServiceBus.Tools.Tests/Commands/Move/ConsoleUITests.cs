using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.MessageMover.Tests;

public class MoveOutputRenderingTests
{
    [Fact]
    public void Render_TsvOutput_UsesCompactKubectlStyle()
    {
        var output = new MoveCommandOutput
        {
            RequestedFormat = OutputFormat.Tsv,
            Source = "orders",
            Target = "orders-replay",
            MessagesProcessed = 100,
            MessagesMoved = 95,
            MessagesFailed = 5,
            AverageMessagesPerSecond = 12.5,
            Goal = "100",
            GoalReached = true,
            Status = "completed_with_failures",
            ElapsedTime = TimeSpan.FromSeconds(8),
            StartedAt = DateTime.Now.AddSeconds(-8),
            CompletedAt = DateTime.Now,
            Authentication = "Azure credential (mynamespace.servicebus.windows.net)",
            Workers = 4,
            ReceiveBatchSize = 25,
            PrefetchCount = 100,
            MaxIdleMinutes = 5,
            SuccessSettlement = "complete",
            FailureSettlement = "abandon",
            DryRun = false,
            ContinueOnError = true
        };
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            new CommandOutputRenderer().Render(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var result = writer.ToString();
        Assert.Contains("NAME", result);
        Assert.Contains("SOURCE", result);
        Assert.Contains("move", result);
        Assert.DoesNotContain("Azure Service Bus Message Mover", result);
    }

    [Fact]
    public void Render_JsonOutput_UsesStandardEnvelope()
    {
        var now = DateTime.Now;
        var output = new MoveCommandOutput
        {
            RequestedFormat = OutputFormat.Json,
            Source = "orders",
            Target = "orders-replay",
            MessagesProcessed = 100,
            MessagesMoved = 95,
            MessagesFailed = 5,
            AverageMessagesPerSecond = 12.5,
            Goal = "100",
            GoalReached = true,
            Status = "completed_with_failures",
            ElapsedTime = TimeSpan.FromSeconds(5),
            StartedAt = now.AddSeconds(-5),
            CompletedAt = now,
            Authentication = "Connection string",
            Workers = 4,
            ReceiveBatchSize = 25,
            PrefetchCount = 100,
            MaxIdleMinutes = 5,
            SuccessSettlement = "complete",
            FailureSettlement = "abandon",
            DryRun = false,
            ContinueOnError = true
        };
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            new CommandOutputRenderer().Render(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var result = writer.ToString();
        Assert.Contains("\"command\": {", result);
        Assert.Contains("\"name\": \"move\"", result);
        Assert.Contains("\"resource\":", result);
        Assert.Contains("\"summary\":", result);
        Assert.Contains("\"details\":", result);
        Assert.DoesNotContain("Press Ctrl+C", result);
    }
}
