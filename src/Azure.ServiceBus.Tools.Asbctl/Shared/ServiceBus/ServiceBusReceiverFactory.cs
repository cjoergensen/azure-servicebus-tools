using Azure.Messaging.ServiceBus;

namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class ServiceBusReceiverFactory
{
    public static ServiceBusReceiver CreateReceiver(
        ServiceBusClient client,
        SourceEndpoint source,
        int prefetchCount,
        string identifier)
    {
        var receiverOptions = new ServiceBusReceiverOptions
        {
            SubQueue = source.SubQueue switch
            {
                SourceSubQueue.None => SubQueue.None,
                SourceSubQueue.DeadLetter => SubQueue.DeadLetter,
                SourceSubQueue.TransferDeadLetter => SubQueue.TransferDeadLetter,
                _ => SubQueue.None
            },
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            PrefetchCount = prefetchCount,
            Identifier = identifier
        };

        if (source.Kind == SourceEntityKind.Queue)
        {
            return client.CreateReceiver(source.EntityName, receiverOptions);
        }

        return client.CreateReceiver(
            source.EntityName,
            source.SubscriptionName!,
            receiverOptions);
    }
}
