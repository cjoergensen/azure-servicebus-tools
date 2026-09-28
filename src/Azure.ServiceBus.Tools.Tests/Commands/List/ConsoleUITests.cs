using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.EntityLister;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Azure.ServiceBus.Tools.EntityLister.Tests;

public class ListOutputRenderingTests
{
    [Fact]
    public void Render_JsonOutput_UsesStandardEnvelope()
    {
        var output = new ListCommandOutput
        {
            RequestedFormat = OutputFormat.Json,
            Kind = ListEntityKind.Queues,
            TopicName = null,
            Queues =
            [
                new QueueListEntry("orders", EntityStatus.Active, 10, 1, 2, 13, 1024, 1024)
            ],
            Topics = [],
            Subscriptions = [],
            Authentication = "Connection string"
        };
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            new CommandOutputRenderer().Render(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var result = writer.ToString();
        Assert.Contains("\"command\": {", result);
        Assert.Contains("\"name\": \"list\"", result);
        Assert.Contains("\"resource\":", result);
        Assert.Contains("\"summary\":", result);
        Assert.Contains("\"details\":", result);
        Assert.Contains("\"items\":", result);
    }

    [Fact]
    public void Render_TsvOutput_UsesCompactHeaders()
    {
        var output = new ListCommandOutput
        {
            RequestedFormat = OutputFormat.Tsv,
            Kind = ListEntityKind.Topics,
            TopicName = null,
            Queues = [],
            Topics =
            [
                new TopicListEntry("orders-events", EntityStatus.Active, 3, 20, 2048, 1024)
            ],
            Subscriptions = [],
            Authentication = "Connection string"
        };
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            new CommandOutputRenderer().Render(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var result = writer.ToString();
        Assert.Contains("TYPE", result);
        Assert.Contains("NAME", result);
        Assert.Contains("topic", result);
        Assert.DoesNotContain("Azure Service Bus —", result);
    }

    [Fact]
    public void Render_TsvOutput_PrintsTabSeparatedRows()
    {
        var output = new ListCommandOutput
        {
            RequestedFormat = OutputFormat.Tsv,
            Kind = ListEntityKind.Topics,
            TopicName = null,
            Queues = [],
            Topics =
            [
                new TopicListEntry("orders-events", EntityStatus.Active, 3, 20, 2048, 1024)
            ],
            Subscriptions = [],
            Authentication = "Connection string"
        };
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            new CommandOutputRenderer().Render(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var result = writer.ToString();
        Assert.Contains("TYPE\tNAME\tSTATUS\tSUBSCRIPTIONS\tSCHEDULED\tSIZE_BYTES\tMAX_MB\tFREE_SPACE", result);
        Assert.Contains("topic\torders-events\tActive\t3\t20\t2,048\t1,024\t100.0%", result);
    }
}
