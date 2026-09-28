using Cjoergensen.Azure.ServiceBus.Tools.Expedite;

namespace Azure.ServiceBus.Tools.Expedite.Tests;

public class CommandLineArgumentsParserTests
{
    private const string ValidConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=name;SharedAccessKey=key";

    [Fact]
    public void ParseArguments_WithMessageId_ParsesSuccessfully()
    {
        var args = new[]
        {
            "expedite",
            "orders",
            "--message-id",
            "orders-1",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal("orders", result.Source!.EntityName);
        Assert.False(result.Behavior!.ExpediteAll);
        Assert.Equal("orders-1", result.Behavior.MessageId);
    }

    [Fact]
    public void ParseArguments_WithAll_ParsesSuccessfully()
    {
        var args = new[]
        {
            "expedite",
            "orders",
            "--all",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.True(result.Behavior!.ExpediteAll);
        Assert.Null(result.Behavior.MessageId);
    }

    [Fact]
    public void ParseArguments_WithAllAndMessageId_ReturnsUsageError()
    {
        var args = new[]
        {
            "expedite",
            "orders",
            "--all",
            "--message-id",
            "orders-1",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("exactly one of --all or --message-id", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithSubQueueSource_ReturnsUsageError()
    {
        var args = new[]
        {
            "expedite",
            "orders/$DeadLetterQueue",
            "--all",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("primary queue sources only", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithoutSelection_ReturnsUsageError()
    {
        var args = new[]
        {
            "expedite",
            "orders",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("exactly one of --all or --message-id", result.ErrorMessage);
    }
}
