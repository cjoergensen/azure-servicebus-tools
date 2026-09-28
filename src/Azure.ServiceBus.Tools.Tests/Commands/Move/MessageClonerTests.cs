using Azure.Messaging.ServiceBus;
using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;

namespace Azure.ServiceBus.Tools.MessageMover.Tests;

public class MessageClonerTests
{
    [Fact]
    public void Clone_CopiesBodyAndStandardProperties()
    {
        var applicationProperties = new Dictionary<string, object>
        {
            ["attempt"] = 3,
            ["reason"] = "poison-message"
        };

        var source = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("payload"),
            messageId: "message-1",
            partitionKey: "session-1",
            sessionId: "session-1",
            replyToSessionId: "reply-session",
            timeToLive: TimeSpan.FromMinutes(5),
            correlationId: "correlation-1",
            subject: "subject-1",
            to: "orders-replay",
            contentType: "application/json",
            replyTo: "reply-queue",
            scheduledEnqueueTime: DateTimeOffset.UtcNow.AddMinutes(1),
            properties: applicationProperties);

        var cloned = MessageCloner.Clone(source);

        Assert.Equal(source.Body.ToString(), cloned.Body.ToString());
        Assert.Equal(source.ContentType, cloned.ContentType);
        Assert.Equal(source.CorrelationId, cloned.CorrelationId);
        Assert.Equal(source.Subject, cloned.Subject);
        Assert.Equal(source.MessageId, cloned.MessageId);
        Assert.Equal(source.TimeToLive, cloned.TimeToLive);
        Assert.Equal(source.SessionId, cloned.SessionId);
        Assert.Equal(source.PartitionKey, cloned.PartitionKey);
        Assert.Equal(source.ReplyTo, cloned.ReplyTo);
        Assert.Equal(source.ReplyToSessionId, cloned.ReplyToSessionId);
        Assert.Equal(source.To, cloned.To);
        Assert.Equal(source.ScheduledEnqueueTime, cloned.ScheduledEnqueueTime);
        Assert.Equal(2, cloned.ApplicationProperties.Count);
        Assert.Equal(3, cloned.ApplicationProperties["attempt"]);
        Assert.Equal("poison-message", cloned.ApplicationProperties["reason"]);
    }

    [Fact]
    public void Clone_CreatesIndependentApplicationPropertiesDictionary()
    {
        var source = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("payload"),
            messageId: "message-1",
            properties: new Dictionary<string, object> { ["key"] = "value" });

        var cloned = MessageCloner.Clone(source);
        cloned.ApplicationProperties["key"] = "changed";
        cloned.ApplicationProperties["new"] = "item";

        Assert.Equal("value", source.ApplicationProperties["key"]);
        Assert.False(source.ApplicationProperties.ContainsKey("new"));
    }
}
