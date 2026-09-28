using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.Shared.Tests;

public class SharedCommandOptionsTests
{
    [Fact]
    public void TryParseAuthenticationOption_WithConnectionString_AssignsValue()
    {
        var reader = new CommandLineTokenReader(["--connection-string", "Endpoint=sb://x/;SharedAccessKey=abc"]);
        var connectionString = default(string);
        var serviceBusNamespace = default(string);

        Assert.True(reader.TryReadNext(out var arg));
        var handled = SharedCommandOptions.TryParseAuthenticationOption(reader, arg, ref connectionString, ref serviceBusNamespace);

        Assert.True(handled);
        Assert.Equal("Endpoint=sb://x/;SharedAccessKey=abc", connectionString);
        Assert.Null(serviceBusNamespace);
    }

    [Fact]
    public void TryParseOutputOption_WithTsv_ParsesOutputFormat()
    {
        var reader = new CommandLineTokenReader(["--output", "tsv"]);
        var outputFormat = OutputFormat.Tsv;

        Assert.True(reader.TryReadNext(out var arg));
        var handled = SharedCommandOptions.TryParseOutputOption(reader, arg, ref outputFormat);

        Assert.True(handled);
        Assert.Equal(OutputFormat.Tsv, outputFormat);
    }

    [Fact]
    public void TryParseWorkerOptions_WithPrefetch_ParsesNonNegativeInt()
    {
        var reader = new CommandLineTokenReader(["--prefetch-count", "42"]);
        var workers = 1;
        var batch = 1;
        var prefetch = 0;

        Assert.True(reader.TryReadNext(out var arg));
        var handled = SharedCommandOptions.TryParseWorkerOptions(reader, arg, ref workers, ref batch, ref prefetch);

        Assert.True(handled);
        Assert.Equal(42, prefetch);
    }

    [Fact]
    public void TokenReader_ReadRequiredValue_ThrowsWhenMissing()
    {
        var reader = new CommandLineTokenReader(["--output"]);
        Assert.True(reader.TryReadNext(out var arg));

        var ex = Assert.Throws<ArgumentException>(() => reader.ReadRequiredValue(arg));

        Assert.Contains("Missing value", ex.Message);
    }
}
