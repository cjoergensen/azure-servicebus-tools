using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Schedule.Commands;

public sealed class ScheduleCommand : IAsbCommand<ScheduleCommandOutput>
{
    public string Name => "schedule";
    public string Description => "Schedule queue messages for later processing";

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

        var channel = Channel.CreateUnbounded<CommandUpdate>(
            new UnboundedChannelOptions { SingleWriter = false });

        var workTask = RunWorkAsync(arguments, channel.Writer, ct);

        await foreach (var update in channel.Reader.ReadAllAsync(ct))
            yield return update;

        await workTask;
    }

    private static async Task RunWorkAsync(
        CommandLineArgumentsParser.ArgumentParsingResult arguments,
        ChannelWriter<CommandUpdate> writer,
        CancellationToken ct)
    {
        var startedAt = DateTime.Now;
        try
        {
            var source = arguments.Source!;
            var adminClient = new ServiceBusAdminClient(
                arguments.ServiceBusConnectionString, arguments.ServiceBusNamespace);
            var initialCount = await adminClient.TryGetSourceMessageCountAsync(source);

            var selection = arguments.Behavior!.ScheduleAll ? "all" : $"message-id:{arguments.Behavior.MessageId}";
            writer.TryWrite(new OperationHeader(
                CommandLine: $"schedule source={source.DisplayName} auth={ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace)}",
                Config: $"config select={selection} at={arguments.Behavior.ScheduleAtUtc:yyyy-MM-ddTHH:mm:ssZ} batch={arguments.MaxMessagesPerBatch} prefetch={arguments.PrefetchCount}",
                StartedAt: startedAt,
                TotalMessages: arguments.Behavior.ScheduleAll ? initialCount : 1,
                OutputFormat: arguments.OutputFormat));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var totalScheduled = 0;
            var totalFailed = 0;

            var progressTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    writer.TryWrite(new OperationProgress(
                        Volatile.Read(ref totalScheduled) + Volatile.Read(ref totalFailed),
                        Volatile.Read(ref totalScheduled),
                        Volatile.Read(ref totalFailed)));
                    try { await Task.Delay(1000, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, CancellationToken.None);

            try
            {
                await using var scheduler = new ServiceBusMessageScheduler(
                    arguments.ServiceBusConnectionString,
                    arguments.ServiceBusNamespace,
                    source,
                    arguments.Behavior!,
                    arguments.MaxMessagesPerBatch,
                    arguments.PrefetchCount,
                    arguments.Identifier,
                    initialCount);

                await foreach (var result in scheduler.ScheduleMessagesAsync(cts.Token))
                {
                    if (result.IsSuccess)
                        Interlocked.Increment(ref totalScheduled);
                    else
                    {
                        Interlocked.Increment(ref totalFailed);
                        writer.TryWrite(new OperationError(result.ErrorMessage!));
                        if (!arguments.Behavior!.ContinueOnError) break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                cts.Cancel();
                await progressTask;

                var scheduledAtEnd = Volatile.Read(ref totalScheduled);
                var failedAtEnd = Volatile.Read(ref totalFailed);
                var processedAtEnd = scheduledAtEnd + failedAtEnd;
                var completedAt = DateTime.Now;
                var duration = completedAt - startedAt;
                var avgRate = duration.TotalSeconds > 0
                    ? Math.Round(processedAtEnd / duration.TotalSeconds, 1) : 0;

                int? goal = arguments.Behavior!.ScheduleAll
                    ? ConsoleProgressReporter.ResolveGoal(null, initialCount)
                    : 1;
                var goalReached = !goal.HasValue || processedAtEnd >= goal.Value;
                var goalText = goal.HasValue ? goal.Value.ToString("N0") : "unlimited";
                var statusText = !goalReached ? "not_reached"
                    : failedAtEnd > 0 ? "completed_with_failures" : "completed";

                if (failedAtEnd > 0) Environment.ExitCode = 1;

                writer.TryWrite(new ScheduleCommandOutput
                {
                    RequestedFormat = arguments.OutputFormat,
                    Source = source.DisplayName,
                    MessagesProcessed = processedAtEnd,
                    MessagesScheduled = scheduledAtEnd,
                    MessagesFailed = failedAtEnd,
                    AverageMessagesPerSecond = avgRate,
                    Goal = goalText,
                    GoalReached = goalReached,
                    Status = statusText,
                    ElapsedTime = duration,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Authentication = ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace),
                    Selection = arguments.Behavior.ScheduleAll ? "all" : "single",
                    MessageId = arguments.Behavior.MessageId,
                    ScheduleAtUtc = arguments.Behavior.ScheduleAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ReceiveBatchSize = arguments.MaxMessagesPerBatch,
                    PrefetchCount = arguments.PrefetchCount,
                    ContinueOnError = arguments.Behavior.ContinueOnError
                });
            }
        }
        finally
        {
            writer.Complete();
        }
    }
}
