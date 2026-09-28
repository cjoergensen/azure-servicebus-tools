using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class ForwardTargetPathResolverTests
{
    [Fact]
    public void NormalizeEntityName_WithEntityName_ReturnsTrimmedName()
    {
        var result = ForwardTargetPathResolver.NormalizeEntityName("  orders-forward  ");

        Assert.Equal("orders-forward", result);
    }

    [Fact]
    public void NormalizeEntityName_WithSbUri_ReturnsEntityPathName()
    {
        var result = ForwardTargetPathResolver.NormalizeEntityName("sb://ns.servicebus.windows.net/orders-forward");

        Assert.Equal("orders-forward", result);
    }

    [Fact]
    public void NormalizeEntityName_WithHttpUri_ReturnsEntityPathName()
    {
        var result = ForwardTargetPathResolver.NormalizeEntityName("http://sbemulatorns.servicebus.onebox.windows-int.net/orders-forward");

        Assert.Equal("orders-forward", result);
    }

    [Fact]
    public void NormalizeEntityName_WithNestedPath_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ForwardTargetPathResolver.NormalizeEntityName("sb://ns.servicebus.windows.net/topic/subscriptions/sub1"));

        Assert.Contains("Only queue/topic entity names are supported", ex.Message);
    }

    [Fact]
    public void NormalizeEntityName_WithEmptyValue_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ForwardTargetPathResolver.NormalizeEntityName("   "));

        Assert.Contains("Forward target is empty", ex.Message);
    }
}
