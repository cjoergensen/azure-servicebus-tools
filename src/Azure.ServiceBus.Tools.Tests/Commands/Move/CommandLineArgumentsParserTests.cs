using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.MessageMover.Tests;

public class CommandLineArgumentsParserTests
{
    private const string ValidConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=name;SharedAccessKey=key";

    [Fact]
    public void ParseArguments_WithQueueToQueuePaths_UsesExpectedDefaults()
    {
        var args = new[]
        {
            "move",
            "orders-in",
            "orders-out",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(SourceEntityKind.Queue, result.Source!.Kind);
        Assert.Equal("orders-in", result.Source.EntityName);
        Assert.Equal(SourceSubQueue.None, result.Source.SubQueue);
        Assert.Equal("orders-out", result.Target!.EntityName);
        Assert.Equal(SettlementMode.Complete, result.Behavior!.OnSuccess);
        Assert.Equal(SettlementMode.Abandon, result.Behavior.OnFailure);
        Assert.False(result.Behavior.DryRun);
    }

    [Fact]
    public void ParseArguments_WithNamespaceAuth_ParsesSuccessfully()
    {
        var args = new[]
        {
            "move",
            "orders-in",
            "orders-out",
            "--namespace", "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Null(result.ServiceBusConnectionString);
        Assert.Equal("mynamespace.servicebus.windows.net", result.ServiceBusNamespace);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionDeadLetterPath_ParsesSourceCorrectly()
    {
        var args = new[]
        {
            "move",
            "legacy/Subscriptions/orders-sub/$DeadLetterQueue",
            "orders",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(SourceEntityKind.Subscription, result.Source!.Kind);
        Assert.Equal("legacy", result.Source.EntityName);
        Assert.Equal("orders-sub", result.Source.SubscriptionName);
        Assert.Equal(SourceSubQueue.DeadLetter, result.Source.SubQueue);
        Assert.Equal("orders", result.Target!.EntityName);
    }

    [Fact]
    public void ParseArguments_WithDryRun_DefaultsSuccessSettlementToNone()
    {
        var args = new[]
        {
            "move",
            "orders/$DeadLetterQueue",
            "orders",
            "-c", ValidConnectionString,
            "--dry-run"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.True(result.Behavior!.DryRun);
        Assert.Equal(SettlementMode.None, result.Behavior.OnSuccess);
        Assert.Equal(SettlementMode.Abandon, result.Behavior.OnFailure);
    }

    [Fact]
    public void ParseArguments_WithSourceTargetControls_ParsesCorrectly()
    {
        var args = new[]
        {
            "--source", "orders/$DeadLetterQueue",
            "--target", "orders",
            "-c", ValidConnectionString,
            "--workers", "6",
            "--receive-batch-size", "40",
            "--prefetch-count", "120"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(SourceEntityKind.Queue, result.Source!.Kind);
        Assert.Equal("orders", result.Source.EntityName);
        Assert.Equal(SourceSubQueue.DeadLetter, result.Source.SubQueue);
        Assert.Equal("orders", result.Target!.EntityName);
        Assert.Equal(6, result.MaxConcurrency);
        Assert.Equal(40, result.MaxMessagesPerBatch);
        Assert.Equal(120, result.PrefetchCount);
        Assert.Equal(SharedConstants.DefaultMaxIdleMinutes, result.MaxIdleMinutes);
    }

    [Fact]
    public void ParseArguments_WithMaxIdleMinutes_ParsesCorrectly()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--max-idle-minutes", "5"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(5, result.MaxIdleMinutes);
    }

    [Fact]
    public void ParseArguments_WithRemovedLegacyControlNames_ReturnsUsageError()
    {
        var args = new[]
        {
            "--from", "orders",
            "--to", "orders-replay",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("Unknown argument", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithRemovedCustomSyntax_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "queue:orders",
            "queue:orders-replay",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("Invalid Azure Service Bus path", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionTargetPath_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "legacy/Subscriptions/orders-sub",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("target cannot be a subscription path", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithoutAuth_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("Authentication is required", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithBothAuthModes_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--namespace", "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("not both", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithTooHighConcurrency_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--workers", "33"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("cannot exceed 32", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithTooHighBatchSize_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--receive-batch-size", "501"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("cannot exceed 500", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithNegativePrefetchCount_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--prefetch-count", "-1"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("zero or greater", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithZeroLimit_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--limit", "0"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("positive number", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithZeroMaxIdleMinutes_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "-c", ValidConnectionString,
            "--max-idle-minutes", "0"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("positive number", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithTransferDeadLetterOnSubscription_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "legacy/Subscriptions/orders-sub/$Transfer/$DeadLetterQueue",
            "orders",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("only supported for queue sources", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithTargetSubQueuePath_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders/$DeadLetterQueue",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("target cannot be a subqueue path", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithPositionalAndSourceTargetFlags_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "--source", "other-source",
            "--target", "other-target",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("either positional paths or --source/--target", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithNamespaceProtocolAndTrailingSlash_NormalizesNamespace()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "--namespace", "sb://mynamespace.servicebus.windows.net/"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal("mynamespace.servicebus.windows.net", result.ServiceBusNamespace);
    }

    [Fact]
    public void ParseArguments_WithInvalidNamespaceHost_ReturnsUsageError()
    {
        var args = new[]
        {
            "move",
            "orders",
            "orders-replay",
            "--namespace", "mynamespace.windows.net"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("must be a valid namespace host", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithDryRunAndExplicitSuccessSettlement_UsesExplicitSettlement()
    {
        var args = new[]
        {
            "move",
            "orders/$DeadLetterQueue",
            "orders",
            "-c", ValidConnectionString,
            "--dry-run",
            "--success-settlement", "complete"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.True(result.Behavior!.DryRun);
        Assert.Equal(SettlementMode.Complete, result.Behavior.OnSuccess);
    }
}
