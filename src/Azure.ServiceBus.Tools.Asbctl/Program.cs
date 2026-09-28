using Azure;
using Azure.Messaging.ServiceBus;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;
using Cjoergensen.Azure.ServiceBus.Tools.EntityInspector.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.EntityLister;
using Cjoergensen.Azure.ServiceBus.Tools.EntityLister.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.Expedite;
using Cjoergensen.Azure.ServiceBus.Tools.Expedite.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;
using Cjoergensen.Azure.ServiceBus.Tools.MessageMover.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.Purger;
using Cjoergensen.Azure.ServiceBus.Tools.Purger.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.Schedule;
using Cjoergensen.Azure.ServiceBus.Tools.Schedule.Commands;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

var commands = new IAsbCommand[]
{
    new MoveCommand(),
    new PurgeCommand(),
    new InspectCommand(),
    new ListCommand(),
    new ScheduleCommand(),
    new ExpediteCommand()
};

if (ShellCompletion.TryHandle(args, commands))
    return;

if (args.Length == 0)
{
    AsbctlUsage.PrintUsageInstructions(commands);
    return;
}

if (string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase))
{
    AsbctlUsage.PrintUsageInstructions(commands);
    return;
}

var dispatcher = new CommandDispatcher(commands);
if (!dispatcher.TryResolve(args[0], out var command))
{
    AsbctlUsage.PrintUsageInstructions(commands, $"Unknown command: {args[0]}");
    Environment.ExitCode = 1;
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    Console.WriteLine();
    Console.ResetColor();
    Console.WriteLine("Cancellation requested...");
    Console.ResetColor();
    cts.Cancel();
    e.Cancel = true;
};

try
{
    var renderer = new CommandOutputRenderer();
    if (command is IAsbCommand<MoveCommandOutput> moveCommand)
        await ExecuteAndRenderAsync(moveCommand, renderer, args, cts.Token);
    else if (command is IAsbCommand<PurgeCommandOutput> purgeCommand)
        await ExecuteAndRenderAsync(purgeCommand, renderer, args, cts.Token);
    else if (command is IAsbCommand<ScheduleCommandOutput> scheduleCommand)
        await ExecuteAndRenderAsync(scheduleCommand, renderer, args, cts.Token);
    else if (command is IAsbCommand<ExpediteCommandOutput> expediteCommand)
        await ExecuteAndRenderAsync(expediteCommand, renderer, args, cts.Token);
    else if (command is IAsbCommand<ListCommandOutput> listCommand)
        await ExecuteAndRenderAsync(listCommand, renderer, args, cts.Token);
    else if (command is IAsbCommand<InspectCommandOutput> inspectCommand)
        await ExecuteAndRenderAsync(inspectCommand, renderer, args, cts.Token);
    else
    {
        AsbctlUsage.PrintUsageInstructions(commands, $"No output renderer registered for command: {command.Name}");
        Environment.ExitCode = 1;
    }
}
catch (RequestFailedException ex) when (!cts.IsCancellationRequested)
{
    OutputWriter.PrintError($"Azure request failed: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (ServiceBusException ex) when (!cts.IsCancellationRequested)
{
    OutputWriter.PrintError($"Service Bus operation failed: {ex.Message}");
    Environment.ExitCode = 1;
}

static async Task ExecuteAndRenderAsync<TOutput>(
    IAsbCommand<TOutput> command,
    CommandOutputRenderer renderer,
    string[] args,
    CancellationToken ct)
    where TOutput : CommandOutput
{
    TOutput? finalOutput = null;

    await foreach (var update in command.ExecuteAsync(args, ct))
    {
        if (update is TOutput output)
            finalOutput = output;
        else
            renderer.RenderUpdate(update);
    }

    if (finalOutput is not null)
        renderer.Render(finalOutput);
}
