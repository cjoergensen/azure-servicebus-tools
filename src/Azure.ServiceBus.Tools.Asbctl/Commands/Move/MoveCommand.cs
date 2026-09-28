using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Cjoergensen.Azure.ServiceBus.Tools.Asbctl;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.MessageMover.Commands;

public sealed class MoveCommand : IAsbCommand<MoveCommandOutput>
{
    public string Name => "move";
    public string Description => "Move messages between entities";

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
        var sbAdminClient = new ServiceBusAdminClient(
            arguments.ServiceBusConnectionString, arguments.ServiceBusNamespace);
        var source = arguments.Source!;

        try
        {
            try
            {
                var sourceResolution = await sbAdminClient.ResolveAutoForwardSourceAsync(source);
                if (sourceResolution.IsAutoForwarded)
                {
                    writer.TryWrite(new OperationError(sourceResolution.Message!));
                    return;
                }
            }
            catch (ArgumentException ex)
            {
                CommandLineArgumentsParser.PrintUsageInstructions(ex.Message);
                return;
            }

            var totalNumberOfSourceMessages =
                await sbAdminClient.TryGetSourceMessageCountAsync(source);

            var effectiveMaxConcurrency = arguments.MaxConcurrency;
            if (arguments.Behavior!.MaxMessages is { } maxMessages)
            {
                effectiveMaxConcurrency = maxMessages <= arguments.MaxMessagesPerBatch
                    ? 1
                    : Math.Min(arguments.MaxConcurrency, maxMessages);
            }

            if (totalNumberOfSourceMessages is > 0)
            {
                effectiveMaxConcurrency = Math.Min(
                    effectiveMaxConcurrency,
                    (int)Math.Min(totalNumberOfSourceMessages.Value, int.MaxValue));
            }

            effectiveMaxConcurrency = Math.Max(1, effectiveMaxConcurrency);

            writer.TryWrite(new OperationHeader(
                CommandLine: $"move source={source.DisplayName} target={arguments.Target!.DisplayName} auth={ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace)}",
                Config: $"config workers={effectiveMaxConcurrency} batch={arguments.MaxMessagesPerBatch} prefetch={arguments.PrefetchCount} max-idle={arguments.MaxIdleMinutes}m goal={(arguments.Behavior!.MaxMessages?.ToString("N0") ?? "unlimited")}",
                StartedAt: startedAt,
                TotalMessages: totalNumberOfSourceMessages,
                OutputFormat: arguments.OutputFormat));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var totalMoved = 0;
            var totalFailed = 0;
            var remainingMessageSlots = arguments.Behavior!.MaxMessages ?? int.MaxValue;

            bool CanProcessNextMessage()
            {
                if (!arguments.Behavior!.MaxMessages.HasValue) return true;
                while (true)
                {
                    var current = Volatile.Read(ref remainingMessageSlots);
                    if (current <= 0) return false;
                    if (Interlocked.CompareExchange(ref remainingMessageSlots, current - 1, current) == current)
                        return true;
                }
            }

            var progressTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    writer.TryWrite(new OperationProgress(
                        Volatile.Read(ref totalMoved) + Volatile.Read(ref totalFailed),
                        Volatile.Read(ref totalMoved),
                        Volatile.Read(ref totalFailed)));
                    try { await Task.Delay(1000, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, CancellationToken.None);

            var limitNotReached = false;
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = effectiveMaxConcurrency,
                    CancellationToken = cts.Token
                };
                DateTime? idleSinceUtc = null;
                var idleWindow = TimeSpan.FromMinutes(arguments.MaxIdleMinutes);

                while (!cts.Token.IsCancellationRequested)
                {
                    var processedAtPassStart =
                        Volatile.Read(ref totalMoved) + Volatile.Read(ref totalFailed);

                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, effectiveMaxConcurrency),
                        parallelOptions,
                        async (workerIndex, cancellationToken) =>
                        {
                            try
                            {
                                var workerIdentifier = $"{arguments.Identifier}-{workerIndex + 1:D2}";
                                await using var mover = new ServiceBusMessageMover(
                                    arguments.ServiceBusConnectionString,
                                    arguments.ServiceBusNamespace,
                                    source,
                                    arguments.Target!,
                                    arguments.Behavior!,
                                    arguments.MaxMessagesPerBatch,
                                    arguments.PrefetchCount,
                                    workerIdentifier,
                                    arguments.Behavior!.MaxMessages.HasValue ? CanProcessNextMessage : null);

                                await foreach (var result in mover.MoveMessagesAsync(cancellationToken))
                                {
                                    if (result.IsSuccess)
                                        Interlocked.Increment(ref totalMoved);
                                    else
                                    {
                                        Interlocked.Increment(ref totalFailed);
                                        if (!arguments.Behavior!.ContinueOnError)
                                        {
                                            cts.Cancel();
                                            break;
                                        }
                                    }

                                    var processed = Volatile.Read(ref totalMoved) + Volatile.Read(ref totalFailed);
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

                    if (cts.Token.IsCancellationRequested) break;

                    if (arguments.Behavior!.MaxMessages is { } configuredMaxMessages &&
                        (Volatile.Read(ref totalMoved) + Volatile.Read(ref totalFailed)) >= configuredMaxMessages)
                        break;

                    var processedAfterPass = Volatile.Read(ref totalMoved) + Volatile.Read(ref totalFailed);
                    if (processedAfterPass > processedAtPassStart)
                    {
                        idleSinceUtc = null;
                        continue;
                    }

                    idleSinceUtc ??= DateTime.UtcNow;
                    var idleElapsed = DateTime.UtcNow - idleSinceUtc.Value;
                    if (idleElapsed < idleWindow)
                    {
                        try { await Task.Delay(SharedConstants.EmptyReceiveBackoffMilliseconds, cts.Token); }
                        catch (OperationCanceledException) { break; }
                        continue;
                    }

                    var sourceCount = await sbAdminClient.TryGetSourceMessageCountAsync(source);
                    if (sourceCount is > 0)
                    {
                        idleSinceUtc = DateTime.UtcNow;
                        try { await Task.Delay(SharedConstants.EmptyReceiveBackoffMilliseconds, cts.Token); }
                        catch (OperationCanceledException) { break; }
                        continue;
                    }

                    break;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                cts.Cancel();
                await progressTask;

                var movedAtEnd = Volatile.Read(ref totalMoved);
                var failedAtEnd = Volatile.Read(ref totalFailed);
                var processedAtEnd = movedAtEnd + failedAtEnd;

                if (arguments.Behavior!.MaxMessages is { } maxConfiguredMessages &&
                    processedAtEnd < maxConfiguredMessages)
                {
                    OutputWriter.PrintWarning(
                        $"Stopped after processing {processedAtEnd} message(s), below --limit {maxConfiguredMessages}. " +
                        $"No more receivable messages were returned before max idle timeout ({arguments.MaxIdleMinutes} minute(s)).");
                    limitNotReached = true;
                }

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

                writer.TryWrite(new MoveCommandOutput
                {
                    RequestedFormat = arguments.OutputFormat,
                    Source = source.DisplayName,
                    Target = arguments.Target!.DisplayName,
                    MessagesProcessed = processedAtEnd,
                    MessagesMoved = movedAtEnd,
                    MessagesFailed = failedAtEnd,
                    AverageMessagesPerSecond = avgRate,
                    Goal = goalText,
                    GoalReached = goalReached,
                    Status = statusText,
                    ElapsedTime = duration,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Authentication = ConsoleUiHelpers.FormatAuthentication(arguments.ServiceBusNamespace),
                    Workers = effectiveMaxConcurrency,
                    ReceiveBatchSize = arguments.MaxMessagesPerBatch,
                    PrefetchCount = arguments.PrefetchCount,
                    MaxIdleMinutes = arguments.MaxIdleMinutes,
                    SuccessSettlement = arguments.Behavior!.OnSuccess.ToString().ToLowerInvariant(),
                    FailureSettlement = arguments.Behavior.OnFailure.ToString().ToLowerInvariant(),
                    DryRun = arguments.Behavior.DryRun,
                    ContinueOnError = arguments.Behavior.ContinueOnError
                });

                if (limitNotReached)
                    Environment.ExitCode = 2;
            }
        }
        finally
        {
            writer.Complete();
        }
    }
}
