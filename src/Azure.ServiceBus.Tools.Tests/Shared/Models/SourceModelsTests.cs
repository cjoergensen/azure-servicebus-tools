using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class SourceModelsTests
{
    [Fact]
    public void SourceEndpoint_DisplayName_ForQueue_IsQueueName()
    {
        var source = new SourceEndpoint(SourceEntityKind.Queue, "orders", null, SourceSubQueue.None);

        Assert.Equal("orders", source.DisplayName);
    }

    [Fact]
    public void SourceEndpoint_DisplayName_ForQueueDeadLetter_HasDeadLetterSuffix()
    {
        var source = new SourceEndpoint(SourceEntityKind.Queue, "orders", null, SourceSubQueue.DeadLetter);

        Assert.Equal("orders/$DeadLetterQueue", source.DisplayName);
    }

    [Fact]
    public void SourceEndpoint_DisplayName_ForQueueTransferDeadLetter_HasTransferDeadLetterSuffix()
    {
        var source = new SourceEndpoint(SourceEntityKind.Queue, "orders", null, SourceSubQueue.TransferDeadLetter);

        Assert.Equal("orders/$Transfer/$DeadLetterQueue", source.DisplayName);
    }

    [Fact]
    public void SourceEndpoint_DisplayName_ForSubscriptionDeadLetter_HasSubscriptionPathAndSuffix()
    {
        var source = new SourceEndpoint(
            SourceEntityKind.Subscription,
            "legacy",
            "orders-sub",
            SourceSubQueue.DeadLetter);

        Assert.Equal("legacy/Subscriptions/orders-sub/$DeadLetterQueue", source.DisplayName);
    }
}
