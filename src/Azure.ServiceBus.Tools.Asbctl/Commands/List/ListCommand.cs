using System.Runtime.CompilerServices;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.EntityLister.Commands;

public sealed class ListCommand : IAsbCommand<ListCommandOutput>
{
    public string Name => "list";
    public string Description => "List queues, topics, or subscriptions with key properties";

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

        var lister = new ServiceBusEntityLister(
            arguments.ServiceBusConnectionString, arguments.ServiceBusNamespace);

        var result = arguments.EntityKind switch
        {
            ListEntityKind.Queues => await lister.ListQueuesAsync(),
            ListEntityKind.Topics => await lister.ListTopicsAsync(),
            ListEntityKind.Subscriptions => await lister.ListSubscriptionsAsync(arguments.TopicName!),
            _ => throw new InvalidOperationException("Unknown entity kind.")
        };

        yield return new ListCommandOutput
        {
            RequestedFormat = arguments.OutputFormat,
            Kind = result.Kind,
            TopicName = result.TopicName,
            Queues = result.Queues,
            Topics = result.Topics,
            Subscriptions = result.Subscriptions,
            Authentication = ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace)
        };
    }
}
