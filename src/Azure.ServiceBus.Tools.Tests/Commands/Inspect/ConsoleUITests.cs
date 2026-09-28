using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;

namespace Azure.ServiceBus.Tools.EntityInspector.Tests;

public class InspectOutputRenderingTests
{
    [Fact]
    public void BuildConsolidatedTableRows_IncludesCoreRowsFirst()
    {
        var summaryRows = new List<(string Field, string Value)>
        {
            ("TOTAL", "10"),
            ("ACTIVE", "9")
        };
        var detailRows = new List<(string Field, string Value)>
        {
            ("AUTH", "Connection string"),
            ("MAX_DELIVERY", "10")
        };

        var rows = CommandOutputRenderer.BuildInspectConsolidatedTableRows("queue", "orders", "Active", summaryRows, detailRows);

        Assert.Equal(("NAME", "inspect"), rows[0]);
        Assert.Equal(("TYPE", "queue"), rows[1]);
        Assert.Equal(("PATH", "orders"), rows[2]);
        Assert.Equal(("STATUS", "Active"), rows[3]);
        Assert.Equal(("TOTAL", "10"), rows[4]);
        Assert.Equal(("ACTIVE", "9"), rows[5]);
        Assert.Equal(("AUTH", "Connection string"), rows[6]);
        Assert.Equal(("MAX_DELIVERY", "10"), rows[7]);
    }

    [Fact]
    public void BuildConsolidatedTableRows_PreservesAllPayloadRows()
    {
        var summaryRows = new List<(string Field, string Value)>
        {
            ("SUBSCRIPTIONS", "3"),
            ("SCHEDULED", "2")
        };
        var detailRows = new List<(string Field, string Value)>
        {
            ("AUTH", "Azure credential"),
            ("SIZE_BYTES", "2,048")
        };

        var rows = CommandOutputRenderer.BuildInspectConsolidatedTableRows("topic", "orders-events", "Active", summaryRows, detailRows);

        Assert.Equal(8, rows.Count);
        Assert.Contains(("SUBSCRIPTIONS", "3"), rows);
        Assert.Contains(("SCHEDULED", "2"), rows);
        Assert.Contains(("AUTH", "Azure credential"), rows);
        Assert.Contains(("SIZE_BYTES", "2,048"), rows);
    }

    [Fact]
    public void BuildTsvLines_ReturnsHeaderRowThenValuesRow()
    {
        var rows = new List<(string Field, string Value)>
        {
            ("NAME", "inspect"),
            ("TYPE", "queue"),
            ("PATH", "orders")
        };

        var lines = CommandOutputRenderer.BuildInspectTsvLines(rows);

        Assert.Equal(2, lines.Count);
        Assert.Equal("NAME\tTYPE\tPATH", lines[0]);
        Assert.Equal("inspect\tqueue\torders", lines[1]);
    }
}
