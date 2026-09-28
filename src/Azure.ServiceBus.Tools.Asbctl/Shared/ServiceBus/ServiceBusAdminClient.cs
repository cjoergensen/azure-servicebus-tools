using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public class ServiceBusAdminClient(string? connectionString, string? serviceBusNamespace)
{
    private readonly ServiceBusAdministrationClient adminClient =
        ServiceBusClientFactory.CreateAdministrationClient(connectionString, serviceBusNamespace);

    public async Task<long?> TryGetSourceMessageCountAsync(SourceEndpoint source)
    {
        try
        {
            if (source.Kind == SourceEntityKind.Queue)
            {
                var queue = await adminClient.GetQueueRuntimePropertiesAsync(source.EntityName);
                return source.SubQueue switch
                {
                    SourceSubQueue.None => queue.Value.ActiveMessageCount,
                    SourceSubQueue.DeadLetter => queue.Value.DeadLetterMessageCount,
                    SourceSubQueue.TransferDeadLetter => queue.Value.TransferDeadLetterMessageCount,
                    _ => null
                };
            }

            var subscription = await adminClient.GetSubscriptionRuntimePropertiesAsync(source.EntityName, source.SubscriptionName!);
            return source.SubQueue switch
            {
                SourceSubQueue.None => subscription.Value.ActiveMessageCount,
                SourceSubQueue.DeadLetter => subscription.Value.DeadLetterMessageCount,
                _ => null
            };
        }
        catch (ServiceBusException)
        {
            return null;
        }
    }

    public async Task<SourceAutoForwardResolution> ResolveAutoForwardSourceAsync(SourceEndpoint source)
    {
        string? forwardTarget;

        if (source.Kind == SourceEntityKind.Queue)
        {
            var queue = await adminClient.GetQueueAsync(source.EntityName);
            forwardTarget = source.SubQueue switch
            {
                SourceSubQueue.None => queue.Value.ForwardTo,
                SourceSubQueue.DeadLetter => queue.Value.ForwardDeadLetteredMessagesTo,
                SourceSubQueue.TransferDeadLetter => queue.Value.ForwardDeadLetteredMessagesTo,
                _ => null
            };
        }
        else
        {
            var subscription = await adminClient.GetSubscriptionAsync(source.EntityName, source.SubscriptionName!);
            forwardTarget = source.SubQueue switch
            {
                SourceSubQueue.None => subscription.Value.ForwardTo,
                SourceSubQueue.DeadLetter => subscription.Value.ForwardDeadLetteredMessagesTo,
                _ => null
            };
        }

        if (string.IsNullOrWhiteSpace(forwardTarget))
        {
            return new SourceAutoForwardResolution(source, false, null);
        }

        var forwardedEntity = ForwardTargetPathResolver.NormalizeEntityName(forwardTarget);
        if (await adminClient.QueueExistsAsync(forwardedEntity))
        {
            var effectiveSource = new SourceEndpoint(SourceEntityKind.Queue, forwardedEntity, null, SourceSubQueue.None);
            var message =
                $"Source '{source.DisplayName}' has auto-forwarding enabled to '{forwardedEntity}'. " +
                $"Receiving from auto-forwarded sources is not supported. " +
                $"Rerun with --source '{effectiveSource.DisplayName}'.";
            return new SourceAutoForwardResolution(effectiveSource, true, message);
        }

        if (await adminClient.TopicExistsAsync(forwardedEntity))
        {
            throw new ArgumentException(
                $"Source '{source.DisplayName}' is auto-forwarded to topic '{forwardedEntity}'. " +
                "Receiving directly from a topic isn't supported. Use a subscription path on that topic.");
        }

        throw new ArgumentException(
            $"Source '{source.DisplayName}' is auto-forwarded to '{forwardedEntity}', " +
            "but that destination entity was not found.");
    }

    public sealed record SourceAutoForwardResolution(
        SourceEndpoint EffectiveSource,
        bool IsAutoForwarded,
        string? Message);
}
