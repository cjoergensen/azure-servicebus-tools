using Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.EntityInspector.Tests;

public class CommandLineArgumentsParserTests
{
    [Fact]
    public void ParseArguments_WithQueuePositionalPath_ParsesEntity()
    {
        var args = new[]
        {
            "inspect",
            "orders",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal("mynamespace.servicebus.windows.net", result.ServiceBusNamespace);
        Assert.Equal(InspectEntityKind.Entity, result.Endpoint!.Kind);
        Assert.Equal("orders", result.Endpoint.EntityName);
        Assert.Equal(SourceSubQueue.None, result.Endpoint.SubQueue);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionPositionalPath_ParsesSubscription()
    {
        var args = new[]
        {
            "inspect",
            "orders-events/Subscriptions/replay-sub",
            "--connection-string",
            "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=key"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(InspectEntityKind.Subscription, result.Endpoint!.Kind);
        Assert.Equal("orders-events", result.Endpoint.EntityName);
        Assert.Equal("replay-sub", result.Endpoint.SubscriptionName);
        Assert.Equal(SourceSubQueue.None, result.Endpoint.SubQueue);
    }

    [Fact]
    public void ParseArguments_WithEntityOption_ParsesEntity()
    {
        var args = new[]
        {
            "--entity",
            "orders-events",
            "--namespace",
            "mynamespace.servicebus.windows.net"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal("orders-events", result.Endpoint!.DisplayName);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionDeadLetterSuffix_ParsesSubQueue()
    {
        var args = new[]
        {
            "inspect",
            "orders-events/Subscriptions/replay-sub/$DeadLetterQueue",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(InspectEntityKind.Subscription, result.Endpoint!.Kind);
        Assert.Equal("orders-events", result.Endpoint.EntityName);
        Assert.Equal("replay-sub", result.Endpoint.SubscriptionName);
        Assert.Equal(SourceSubQueue.DeadLetter, result.Endpoint.SubQueue);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionTransferDeadLetterSuffix_ShowsHelpWithError()
    {
        var args = new[]
        {
            "inspect",
            "orders-events/Subscriptions/replay-sub/$Transfer/$DeadLetterQueue",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("only supported for queue paths", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithInvalidPathSyntax_ShowsHelpWithError()
    {
        var args = new[]
        {
            "inspect",
            "orders/topics/replay",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("Invalid Azure Service Bus path", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithMixedPositionalAndEntityOption_ShowsHelpWithError()
    {
        var args = new[]
        {
            "inspect",
            "orders",
            "--entity",
            "orders-events",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("either positional path or --entity", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithoutAuthentication_ShowsHelpWithError()
    {
        var args = new[]
        {
            "inspect",
            "orders"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("either --connection-string or --namespace", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithTsvOutput_ParsesOutputFormat()
    {
        var args = new[]
        {
            "inspect",
            "orders",
            "--namespace",
            "mynamespace",
            "--output",
            "tsv"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(OutputFormat.Tsv, result.OutputFormat);
    }

    [Fact]
    public void ParseArguments_WithUnknownOutput_ShowsHelpWithError()
    {
        var args = new[]
        {
            "inspect",
            "orders",
            "--namespace",
            "mynamespace",
            "--output",
            "yaml"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("Use 'json' or 'tsv'", result.ErrorMessage);
    }
}
