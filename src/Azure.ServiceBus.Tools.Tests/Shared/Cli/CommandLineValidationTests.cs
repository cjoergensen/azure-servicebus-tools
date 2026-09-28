using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class CommandLineValidationTests
{
    private const string ValidConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=name;SharedAccessKey=key";

    [Fact]
    public void ValidateAuthentication_WithConnectionString_Succeeds()
    {
        CommandLineValidation.ValidateAuthentication(ValidConnectionString, null);
    }

    [Fact]
    public void ValidateAuthentication_WithNamespace_Succeeds()
    {
        CommandLineValidation.ValidateAuthentication(null, "mynamespace.servicebus.windows.net");
    }

    [Fact]
    public void ValidateAuthentication_WithoutAuth_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateAuthentication(null, null));

        Assert.Contains("Authentication is required", ex.Message);
    }

    [Fact]
    public void ValidateAuthentication_WithBothAuthModes_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateAuthentication(ValidConnectionString, "mynamespace.servicebus.windows.net"));

        Assert.Contains("not both", ex.Message);
    }

    [Fact]
    public void ValidateAuthentication_WithInvalidConnectionString_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateAuthentication("Endpoint=sb://test/;", null));

        Assert.Contains("Invalid Service Bus connection string format", ex.Message);
    }

    [Fact]
    public void ValidateSourcePathCompatibility_WithSubscriptionTransferDeadLetter_Throws()
    {
        var source = new SourceEndpoint(
            SourceEntityKind.Subscription,
            "legacy",
            "orders-sub",
            SourceSubQueue.TransferDeadLetter);

        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateSourcePathCompatibility(source));

        Assert.Contains("only supported for queue sources", ex.Message);
    }

    [Fact]
    public void ValidateWorkerSettings_WithValidValues_Succeeds()
    {
        CommandLineValidation.ValidateWorkerSettings(8, 25, 0, 100);
    }

    [Fact]
    public void ValidateWorkerSettings_WithTooHighConcurrency_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateWorkerSettings(33, 25, 0, 100));

        Assert.Contains("cannot exceed 32", ex.Message);
    }

    [Fact]
    public void ValidateWorkerSettings_WithTooHighBatchSize_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateWorkerSettings(8, 501, 0, 100));

        Assert.Contains("cannot exceed 500", ex.Message);
    }

    [Fact]
    public void ValidateWorkerSettings_WithNegativePrefetch_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateWorkerSettings(8, 25, -1, 100));

        Assert.Contains("zero or greater", ex.Message);
    }

    [Fact]
    public void ValidateWorkerSettings_WithZeroLimit_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandLineValidation.ValidateWorkerSettings(8, 25, 0, 0));

        Assert.Contains("positive number", ex.Message);
    }
}
