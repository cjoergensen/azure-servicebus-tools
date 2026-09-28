using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class ServiceBusClientFactory
{
    public static ServiceBusClient CreateClient(string? connectionString, string? serviceBusNamespace)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new ServiceBusClient(connectionString);
        }

        if (!string.IsNullOrWhiteSpace(serviceBusNamespace))
        {
            return new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential());
        }

        throw new ArgumentException("Either connection string or Service Bus namespace must be provided.");
    }

    public static ServiceBusAdministrationClient CreateAdministrationClient(string? connectionString, string? serviceBusNamespace)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new ServiceBusAdministrationClient(connectionString);
        }

        if (!string.IsNullOrWhiteSpace(serviceBusNamespace))
        {
            return new ServiceBusAdministrationClient(serviceBusNamespace, new DefaultAzureCredential());
        }

        throw new ArgumentException("Either connection string or Service Bus namespace must be provided.");
    }
}
