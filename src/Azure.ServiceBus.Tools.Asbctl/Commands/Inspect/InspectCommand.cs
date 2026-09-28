using System.Runtime.CompilerServices;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityInspector.Commands;

public sealed class InspectCommand : IAsbCommand<InspectCommandOutput>
{
    public string Name => "inspect";
    public string Description => "Read queue/topic/subscription properties";

    public async IAsyncEnumerable<CommandUpdate> ExecuteAsync(
        string[] args,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var arguments = CommandLineArgumentsParser.ParseArguments(args);
        if (arguments.ShowHelp)
        {
            CommandLineArgumentsParser.PrintUsageInstructions(arguments.ErrorMessage);
            yield break;
        }

        var inspector = new ServiceBusEntityInspector(
            arguments.ServiceBusConnectionString, arguments.ServiceBusNamespace);

        var result = await inspector.InspectAsync(arguments.Endpoint!);

        yield return new InspectCommandOutput
        {
            RequestedFormat = arguments.OutputFormat,
            Kind = result.Kind,
            Path = result.Endpoint.DisplayName,
            Authentication = ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace),
            QueueProperties = result.QueueProperties,
            QueueRuntimeProperties = result.QueueRuntimeProperties,
            TopicProperties = result.TopicProperties,
            TopicRuntimeProperties = result.TopicRuntimeProperties,
            SubscriptionProperties = result.SubscriptionProperties,
            SubscriptionRuntimeProperties = result.SubscriptionRuntimeProperties
        };
    }
}
