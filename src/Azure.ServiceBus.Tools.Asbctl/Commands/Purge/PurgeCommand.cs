using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Purger.Commands;

public sealed class PurgeCommand : IAsbCommand<PurgeCommandOutput>
{
    public string Name => "purge";
    public string Description => "Purge messages from a source entity";

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
            var sbAdminClient = new ServiceBusAdminClient(
                arguments.ServiceBusConnectionString, arguments.ServiceBusNamespace);
            var totalNumberOfSourceMessages =
                await sbAdminClient.TryGetSourceMessageCountAsync(arguments.Source!);

            writer.TryWrite(new OperationHeader(
                CommandLine: $"purge source={arguments.Source!.DisplayName} auth={ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace)}",
                Config: $"config workers={arguments.MaxConcurrency} batch={arguments.MaxMessagesPerBatch} prefetch={arguments.PrefetchCount} goal={(arguments.Behavior!.MaxMessages?.ToString("N0") ?? "unlimited")}",
                StartedAt: startedAt,
                TotalMessages: totalNumberOfSourceMessages,
                OutputFormat: arguments.OutputFormat));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var totalPurged = 0;
            var totalFailed = 0;
            var totalProcessed = 0;

            var progressTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    writer.TryWrite(new OperationProgress(
                        Volatile.Read(ref totalPurged) + Volatile.Read(ref totalFailed),
                        Volatile.Read(ref totalPurged),
                        Volatile.Read(ref totalFailed)));
                    try { await Task.Delay(1000, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, CancellationToken.None);

            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = arguments.MaxConcurrency,
                    CancellationToken = cts.Token
                };

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, arguments.MaxConcurrency),
                    parallelOptions,
                    async (workerIndex, cancellationToken) =>
                    {
                        try
                        {
                            var workerIdentifier = $"{arguments.Identifier}-{workerIndex + 1:D2}";
                            await using var purger = new ServiceBusMessagePurger(
                                arguments.ServiceBusConnectionString,
                                arguments.ServiceBusNamespace,
                                arguments.Source!,
                                arguments.MaxMessagesPerBatch,
                                arguments.PrefetchCount,
                                workerIdentifier);

                            await foreach (var result in purger.PurgeMessagesAsync(cancellationToken))
                            {
                                if (result.IsSuccess)
                                    Interlocked.Increment(ref totalPurged);
                                else
                                {
                                    Interlocked.Increment(ref totalFailed);
                                    if (!arguments.Behavior!.ContinueOnError)
                                    {
                                        cts.Cancel();
                                        break;
                                    }
                                }

                                var processed = Interlocked.Increment(ref totalProcessed);
                                if (arguments.Behavior!.MaxMessages.HasValue &&
                                    processed >= arguments.Behavior.MaxMessages.Value)
                                {
                                    cts.Cancel();
                                    break;
                                }
                            }
                        }
                        catch (OperationCanceledException) { }
                    });
            }
            catch (OperationCanceledException) { }
            finally
            {
                cts.Cancel();
                await progressTask;

                var purgedAtEnd = Volatile.Read(ref totalPurged);
                var failedAtEnd = Volatile.Read(ref totalFailed);
                var processedAtEnd = purgedAtEnd + failedAtEnd;
                var completedAt = DateTime.Now;
                var duration = completedAt - startedAt;
                var avgRate = duration.TotalSeconds > 0
                    ? Math.Round(processedAtEnd / duration.TotalSeconds, 1) : 0;
                var goal = ConsoleProgressReporter.ResolveGoal(
                    arguments.Behavior!.MaxMessages, totalNumberOfSourceMessages);
                var goalReached = !goal.HasValue || processedAtEnd >= goal.Value;
                var goalText = goal.HasValue ? goal.Value.ToString("N0") : "unlimited";
                var statusText = !goalReached ? "not_reached"
                    : failedAtEnd > 0 ? "completed_with_failures" : "completed";

                writer.TryWrite(new PurgeCommandOutput
                {
                    RequestedFormat = arguments.OutputFormat,
                    Source = arguments.Source!.DisplayName,
                    MessagesProcessed = processedAtEnd,
                    MessagesPurged = purgedAtEnd,
                    MessagesFailed = failedAtEnd,
                    AverageMessagesPerSecond = avgRate,
                    Goal = goalText,
                    GoalReached = goalReached,
                    Status = statusText,
                    ElapsedTime = duration,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Authentication = ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace),
                    Workers = arguments.MaxConcurrency,
                    ReceiveBatchSize = arguments.MaxMessagesPerBatch,
                    PrefetchCount = arguments.PrefetchCount,
                    ContinueOnError = arguments.Behavior!.ContinueOnError
                });
            }
        }
        finally
        {
            writer.Complete();
        }
    }
}
