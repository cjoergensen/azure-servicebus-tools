using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.MessageMover.Tests;

public class MessageMoverModelsTests
{
    [Fact]
    public void SourceEndpoint_DisplayName_ForQueueWithoutSubQueue_IsQueueName()
    {
        var source = new SourceEndpoint(SourceEntityKind.Queue, "orders", null, SourceSubQueue.None);

        Assert.Equal("orders", source.DisplayName);
    }

    [Fact]
    public void SourceEndpoint_DisplayName_ForQueueDeadLetter_UsesDeadLetterSuffix()
    {
        var source = new SourceEndpoint(SourceEntityKind.Queue, "orders", null, SourceSubQueue.DeadLetter);

        Assert.Equal("orders/$DeadLetterQueue", source.DisplayName);
    }

    [Fact]
    public void SourceEndpoint_DisplayName_ForQueueTransferDeadLetter_UsesTransferDeadLetterSuffix()
    {
        var source = new SourceEndpoint(SourceEntityKind.Queue, "orders", null, SourceSubQueue.TransferDeadLetter);

        Assert.Equal("orders/$Transfer/$DeadLetterQueue", source.DisplayName);
    }

    [Fact]
    public void SourceEndpoint_DisplayName_ForSubscriptionDeadLetter_UsesSubscriptionPathAndSuffix()
    {
        var source = new SourceEndpoint(
            SourceEntityKind.Subscription,
            "legacy",
            "orders-sub",
            SourceSubQueue.DeadLetter);

        Assert.Equal("legacy/Subscriptions/orders-sub/$DeadLetterQueue", source.DisplayName);
    }

    [Fact]
    public void TargetEndpoint_DisplayName_IsEntityName()
    {
        var target = new TargetEndpoint("orders-replay");

        Assert.Equal("orders-replay", target.DisplayName);
    }
}
