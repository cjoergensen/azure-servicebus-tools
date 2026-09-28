namespace Azure.ServiceBus.Tools.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class ServiceBusCollection : ICollectionFixture<ServiceBusIntegrationFixture>
{
    public const string Name = "ServiceBus Emulator";
}
