using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Expedite.Commands;

public sealed class ExpediteCommand : IAsbCommand<ExpediteCommandOutput>
{
    public string Name => "expedite";
    public string Description => "Process scheduled queue messages immediately";

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
            var selection = arguments.Behavior!.ExpediteAll ? "all" : $"message-id:{arguments.Behavior.MessageId}";
            writer.TryWrite(new OperationHeader(
                CommandLine: $"expedite source={arguments.Source!.DisplayName} auth={ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace)}",
                Config: $"config select={selection} peek-batch={arguments.PeekBatchSize} prefetch={arguments.PrefetchCount}",
                StartedAt: startedAt,
                TotalMessages: arguments.Behavior.ExpediteAll ? null : 1,
                OutputFormat: arguments.OutputFormat));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var expedited = 0;
            var failed = 0;

            var progressTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    writer.TryWrite(new OperationProgress(
                        Volatile.Read(ref expedited) + Volatile.Read(ref failed),
                        Volatile.Read(ref expedited),
                        Volatile.Read(ref failed)));
                    try { await Task.Delay(1000, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, CancellationToken.None);

            try
            {
                await using var expediter = new ServiceBusMessageExpediter(
                    arguments.ServiceBusConnectionString,
                    arguments.ServiceBusNamespace,
                    arguments.Source!,
                    arguments.Behavior!,
                    arguments.PeekBatchSize,
                    arguments.PrefetchCount,
                    arguments.Identifier);

                await foreach (var result in expediter.ExpediteMessagesAsync(cts.Token))
                {
                    if (result.IsSuccess)
                        Interlocked.Increment(ref expedited);
                    else
                    {
                        Interlocked.Increment(ref failed);
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

                var expeditedAtEnd = Volatile.Read(ref expedited);
                var failedAtEnd = Volatile.Read(ref failed);
                var processedAtEnd = expeditedAtEnd + failedAtEnd;
                var completedAt = DateTime.Now;
                var duration = completedAt - startedAt;
                var avgRate = duration.TotalSeconds > 0
                    ? Math.Round(processedAtEnd / duration.TotalSeconds, 1) : 0;
                var goalReached = arguments.Behavior!.ExpediteAll || processedAtEnd >= 1;
                var goalText = arguments.Behavior.ExpediteAll ? "unlimited" : "1";
                var statusText = !goalReached ? "not_reached"
                    : failedAtEnd > 0 ? "completed_with_failures" : "completed";

                if (failedAtEnd > 0) Environment.ExitCode = 1;

                writer.TryWrite(new ExpediteCommandOutput
                {
                    RequestedFormat = arguments.OutputFormat,
                    Source = arguments.Source!.DisplayName,
                    MessagesProcessed = processedAtEnd,
                    MessagesExpedited = expeditedAtEnd,
                    MessagesFailed = failedAtEnd,
                    AverageMessagesPerSecond = avgRate,
                    Goal = goalText,
                    GoalReached = goalReached,
                    Status = statusText,
                    ElapsedTime = duration,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Authentication = ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace),
                    Selection = arguments.Behavior.ExpediteAll ? "all" : "single",
                    MessageId = arguments.Behavior.MessageId,
                    PeekBatchSize = arguments.PeekBatchSize,
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
