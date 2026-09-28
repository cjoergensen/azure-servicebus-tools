using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class CommandLineParsingTests
{
    [Fact]
    public void NormalizeNamespace_WithShortName_AppendsServiceBusSuffix()
    {
        var result = CommandLineParsing.NormalizeNamespace("mynamespace");

        Assert.Equal("mynamespace.servicebus.windows.net", result);
    }

    [Fact]
    public void NormalizeNamespace_WithProtocolAndTrailingSlash_Normalizes()
    {
        var result = CommandLineParsing.NormalizeNamespace("sb://mynamespace.servicebus.windows.net/");

        Assert.Equal("mynamespace.servicebus.windows.net", result);
    }

    [Fact]
    public void NormalizeNamespace_WithPathSegments_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineParsing.NormalizeNamespace("mynamespace.servicebus.windows.net/orders"));

        Assert.Contains("must not include entity path segments", ex.Message);
    }

    [Fact]
    public void ParseSourcePath_WithQueuePath_ParsesQueue()
    {
        var source = CommandLineParsing.ParseSourcePath("orders", "missing source");

        Assert.Equal(SourceEntityKind.Queue, source.Kind);
        Assert.Equal("orders", source.EntityName);
        Assert.Equal(SourceSubQueue.None, source.SubQueue);
    }

    [Fact]
    public void ParseSourcePath_WithSubscriptionDeadLetter_ParsesSubscription()
    {
        var source = CommandLineParsing.ParseSourcePath(
            "legacy/Subscriptions/orders-sub/$DeadLetterQueue",
            "missing source");

        Assert.Equal(SourceEntityKind.Subscription, source.Kind);
        Assert.Equal("legacy", source.EntityName);
        Assert.Equal("orders-sub", source.SubscriptionName);
        Assert.Equal(SourceSubQueue.DeadLetter, source.SubQueue);
    }

    [Fact]
    public void ParseSourcePath_WithCustomSyntax_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineParsing.ParseSourcePath("queue:orders", "missing source"));

        Assert.Contains("Invalid Azure Service Bus path", ex.Message);
    }

    [Fact]
    public void ParseTargetEntityPath_WithQueuePath_ReturnsEntityName()
    {
        var target = CommandLineParsing.ParseTargetEntityPath("orders-replay", "missing target");

        Assert.Equal("orders-replay", target);
    }

    [Fact]
    public void ParseTargetEntityPath_WithSubQueue_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineParsing.ParseTargetEntityPath("orders/$DeadLetterQueue", "missing target"));

        Assert.Contains("target cannot be a subqueue path", ex.Message);
    }

    [Fact]
    public void ParseTargetEntityPath_WithSubscriptionPath_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineParsing.ParseTargetEntityPath("legacy/Subscriptions/orders-sub", "missing target"));

        Assert.Contains("target cannot be a subscription path", ex.Message);
    }

    [Fact]
    public void ParsePositiveInt_WithInvalidNumber_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineParsing.ParsePositiveInt("abc", "max messages"));

        Assert.Contains("Must be a number", ex.Message);
    }

    [Fact]
    public void ParseNonNegativeInt_WithNegativeNumber_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineParsing.ParseNonNegativeInt("-1", "prefetch count"));

        Assert.Contains("zero or greater", ex.Message);
    }
}
