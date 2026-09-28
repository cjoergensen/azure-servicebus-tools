using Cjoergensen.Azure.ServiceBus.Tools.Purger;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Purger.Tests;

public class CommandLineArgumentsParserTests
{
    private const string ValidConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=name;SharedAccessKey=key";

    [Fact]
    public void ParseArguments_WithQueuePath_UsesExpectedDefaults()
    {
        var args = new[]
        {
            "purge",
            "orders",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(SourceEntityKind.Queue, result.Source!.Kind);
        Assert.Equal("orders", result.Source.EntityName);
        Assert.Equal(SourceSubQueue.None, result.Source.SubQueue);
        Assert.False(result.Behavior!.ContinueOnError);
        Assert.Null(result.Behavior.MaxMessages);
        Assert.Equal(8, result.MaxConcurrency);
        Assert.Equal(25, result.MaxMessagesPerBatch);
        Assert.Equal(0, result.PrefetchCount);
    }

    [Fact]
    public void ParseArguments_WithNamespaceAuth_ParsesSuccessfully()
    {
        var args = new[]
        {
            "purge",
            "orders",
            "--namespace", "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Null(result.ServiceBusConnectionString);
        Assert.Equal("mynamespace.servicebus.windows.net", result.ServiceBusNamespace);
    }

    [Fact]
    public void ParseArguments_WithNamespaceProtocolAndTrailingSlash_NormalizesNamespace()
    {
        var args = new[]
        {
            "purge",
            "orders",
            "--namespace", "sb://mynamespace.servicebus.windows.net/"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal("mynamespace.servicebus.windows.net", result.ServiceBusNamespace);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionDeadLetterPath_ParsesSourceCorrectly()
    {
        var args = new[]
        {
            "purge",
            "legacy/Subscriptions/orders-sub/$DeadLetterQueue",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(SourceEntityKind.Subscription, result.Source!.Kind);
        Assert.Equal("legacy", result.Source.EntityName);
        Assert.Equal("orders-sub", result.Source.SubscriptionName);
        Assert.Equal(SourceSubQueue.DeadLetter, result.Source.SubQueue);
    }

    [Fact]
    public void ParseArguments_WithSourceFlag_ParsesCorrectly()
    {
        var args = new[]
        {
            "--source", "orders/$DeadLetterQueue",
            "-c", ValidConnectionString,
            "--workers", "6",
            "--receive-batch-size", "40",
            "--prefetch-count", "120",
            "--continue-on-failure",
            "--limit", "200",
            "--client-id-prefix", "CustomPurge"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(SourceEntityKind.Queue, result.Source!.Kind);
        Assert.Equal("orders", result.Source.EntityName);
        Assert.Equal(SourceSubQueue.DeadLetter, result.Source.SubQueue);
        Assert.True(result.Behavior!.ContinueOnError);
        Assert.Equal(200, result.Behavior.MaxMessages);
        Assert.Equal(6, result.MaxConcurrency);
        Assert.Equal(40, result.MaxMessagesPerBatch);
        Assert.Equal(120, result.PrefetchCount);
        Assert.Equal("CustomPurge", result.Identifier);
    }

    [Fact]
    public void ParseArguments_WithPositionalAndSourceFlag_ReturnsUsageError()
    {
        var args = new[]
        {
            "purge",
            "orders",
            "--source", "other-source",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("either positional path or --source", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithTooHighConcurrency_ReturnsUsageError()
    {
        var args = new[]
        {
            "purge",
            "orders",
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
            "purge",
            "orders",
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
            "purge",
            "orders",
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
            "purge",
            "orders",
            "-c", ValidConnectionString,
            "--limit", "0"
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
            "purge",
            "legacy/Subscriptions/orders-sub/$Transfer/$DeadLetterQueue",
            "-c", ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("only supported for queue sources", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithoutAuth_ReturnsUsageError()
    {
        var args = new[]
        {
            "purge",
            "orders"
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
            "purge",
            "orders",
            "-c", ValidConnectionString,
            "--namespace", "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("not both", result.ErrorMessage);
    }
}
