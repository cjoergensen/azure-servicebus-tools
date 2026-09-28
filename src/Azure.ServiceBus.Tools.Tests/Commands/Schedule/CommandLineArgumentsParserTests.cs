using Cjoergensen.Azure.ServiceBus.Tools.Schedule;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Schedule.Tests;

public class CommandLineArgumentsParserTests
{
    private const string ValidConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=name;SharedAccessKey=key";

    [Fact]
    public void ParseArguments_WithMessageIdAndFor_ParsesSuccessfully()
    {
        var args = new[]
        {
            "schedule",
            "orders",
            "--message-id",
            "orders-1",
            "--for",
            "00:30:00",
            "--namespace",
            "mynamespace"
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.Equal(SourceEntityKind.Queue, result.Source!.Kind);
        Assert.Equal("orders", result.Source.EntityName);
        Assert.False(result.Behavior!.ScheduleAll);
        Assert.Equal("orders-1", result.Behavior.MessageId);
        Assert.Equal("mynamespace.servicebus.windows.net", result.ServiceBusNamespace);
    }

    [Fact]
    public void ParseArguments_WithAllAndUntil_ParsesSuccessfully()
    {
        var args = new[]
        {
            "schedule",
            "orders",
            "--all",
            "--until",
            DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"),
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.False(result.ShowHelp);
        Assert.True(result.Behavior!.ScheduleAll);
        Assert.Null(result.Behavior.MessageId);
    }

    [Fact]
    public void ParseArguments_WithAllAndMessageId_ReturnsUsageError()
    {
        var args = new[]
        {
            "schedule",
            "orders",
            "--all",
            "--message-id",
            "orders-1",
            "--for",
            "00:10:00",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("exactly one of --all or --message-id", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithoutSelection_ReturnsUsageError()
    {
        var args = new[]
        {
            "schedule",
            "orders",
            "--for",
            "00:10:00",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("exactly one of --all or --message-id", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithBothForAndUntil_ReturnsUsageError()
    {
        var args = new[]
        {
            "schedule",
            "orders",
            "--all",
            "--for",
            "00:10:00",
            "--until",
            DateTimeOffset.UtcNow.AddMinutes(20).ToString("O"),
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("exactly one of --for", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithPastUntil_ReturnsUsageError()
    {
        var args = new[]
        {
            "schedule",
            "orders",
            "--all",
            "--until",
            DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("must be in the future", result.ErrorMessage);
    }

    [Fact]
    public void ParseArguments_WithSubscriptionSource_ReturnsUsageError()
    {
        var args = new[]
        {
            "schedule",
            "orders-topic/Subscriptions/orders-sub",
            "--message-id",
            "orders-1",
            "--for",
            "00:10:00",
            "-c",
            ValidConnectionString
        };

        var result = CommandLineArgumentsParser.ParseArguments(args);

        Assert.True(result.ShowHelp);
        Assert.Contains("queue sources only", result.ErrorMessage);
    }
}
