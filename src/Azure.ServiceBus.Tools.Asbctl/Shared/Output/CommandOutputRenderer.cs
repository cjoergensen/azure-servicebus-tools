using Azure.Messaging.ServiceBus.Administration;
using Cjoergensen.Azure.ServiceBus.Tools.EntityInspector;
using Cjoergensen.Azure.ServiceBus.Tools.EntityLister;
using Cjoergensen.Azure.ServiceBus.Tools.Expedite;
using Cjoergensen.Azure.ServiceBus.Tools.MessageMover;
using Cjoergensen.Azure.ServiceBus.Tools.Purger;
using Cjoergensen.Azure.ServiceBus.Tools.Schedule;
using Cjoergensen.Azure.ServiceBus.Tools.Shared;

namespace Cjoergensen.Azure.ServiceBus.Tools.Asbctl;

public sealed class CommandOutputRenderer
{
    private ConsoleProgressReporter? progressReporter;
    private bool isTsvFormat;
    private string progressLabel = "processed";

    public void RenderUpdate(CommandUpdate update)
    {
        switch (update)
        {
            case OperationHeader h:
                isTsvFormat = h.OutputFormat == OutputFormat.Tsv;
                progressLabel = ResolveProgressLabel(h.CommandLine);
                if (!isTsvFormat) return;
                progressReporter = new ConsoleProgressReporter(
                    h.StartedAt,
                    progressLabel,
                    () => h.TotalMessages is > 0 ? (int?)Math.Min(h.TotalMessages.Value, int.MaxValue) : null);
                Console.ResetColor();
                Console.WriteLine(h.CommandLine);
                Console.WriteLine(h.Config);
                Console.WriteLine("Press Ctrl+C to stop processing");
                Console.ResetColor();
                break;
            case OperationProgress p:
                if (!isTsvFormat) return;
                progressReporter?.Print(p.Succeeded, p.Failed);
                break;
            case OperationError e:
                OutputWriter.PrintError(e.Message);
                break;
        }
    }

    public void Render(CommandOutput output)
    {
        switch (output)
        {
            case MoveCommandOutput move:
                RenderMove(move);
                break;
            case PurgeCommandOutput purge:
                RenderPurge(purge);
                break;
            case ScheduleCommandOutput schedule:
                RenderSchedule(schedule);
                break;
            case ExpediteCommandOutput expedite:
                RenderExpedite(expedite);
                break;
            case ListCommandOutput list:
                RenderList(list);
                break;
            case InspectCommandOutput inspect:
                RenderInspect(inspect);
                break;
            default:
                OutputWriter.PrintError($"No renderer for output type: {output.GetType().Name}");
                break;
        }
    }

    private static string ResolveProgressLabel(string commandLine)
    {
        var command = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        return command switch
        {
            "move" => "moved",
            "purge" => "purged",
            "schedule" => "scheduled",
            "expedite" => "expedited",
            _ => "processed"
        };
    }

    private static void RenderMove(MoveCommandOutput output)
    {
        switch (output.RequestedFormat)
        {
            case OutputFormat.Json:
                OutputWriter.PrintJsonEnvelope(
                    command: "move",
                    resource: new { source = output.Source, target = output.Target },
                    startedAt: output.StartedAt,
                    completedAt: output.CompletedAt,
                    status: output.Status,
                    summary: new
                    {
                        processed = output.MessagesProcessed,
                        moved = output.MessagesMoved,
                        failed = output.MessagesFailed,
                        averageRate = output.AverageMessagesPerSecond,
                        goal = output.Goal,
                        goalReached = output.GoalReached
                    },
                    details: new
                    {
                        authentication = output.Authentication,
                        execution = new
                        {
                            workers = output.Workers,
                            receiveBatchSize = output.ReceiveBatchSize,
                            prefetch = output.PrefetchCount,
                            maxIdleMinutes = output.MaxIdleMinutes
                        },
                        behavior = new
                        {
                            successSettlement = output.SuccessSettlement,
                            failureSettlement = output.FailureSettlement,
                            dryRun = output.DryRun,
                            continueOnError = output.ContinueOnError
                        }
                    });
                break;
            case OutputFormat.Tsv:
                OutputWriter.PrintTsv(
                    ["NAME", "SOURCE", "TARGET", "PROCESSED", "MOVED", "FAILED", "RATE", "GOAL", "STATUS", "DURATION"],
                    [[
                        "move",
                        output.Source,
                        output.Target,
                        output.MessagesProcessed.ToString("N0"),
                        output.MessagesMoved.ToString("N0"),
                        output.MessagesFailed.ToString("N0"),
                        $"{output.AverageMessagesPerSecond:N1}/s",
                        output.Goal,
                        output.Status,
                        OutputWriter.FormatDuration(output.ElapsedTime)
                    ]]);
                break;
        }
    }

    private static void RenderPurge(PurgeCommandOutput output)
    {
        switch (output.RequestedFormat)
        {
            case OutputFormat.Json:
                OutputWriter.PrintJsonEnvelope(
                    command: "purge",
                    resource: new { source = output.Source },
                    startedAt: output.StartedAt,
                    completedAt: output.CompletedAt,
                    status: output.Status,
                    summary: new
                    {
                        processed = output.MessagesProcessed,
                        purged = output.MessagesPurged,
                        failed = output.MessagesFailed,
                        averageRate = output.AverageMessagesPerSecond,
                        goal = output.Goal,
                        goalReached = output.GoalReached
                    },
                    details: new
                    {
                        authentication = output.Authentication,
                        execution = new
                        {
                            workers = output.Workers,
                            receiveBatchSize = output.ReceiveBatchSize,
                            prefetch = output.PrefetchCount
                        },
                        behavior = new
                        {
                            continueOnError = output.ContinueOnError
                        }
                    });
                break;
            case OutputFormat.Tsv:
                OutputWriter.PrintTsv(
                    ["NAME", "SOURCE", "PROCESSED", "PURGED", "FAILED", "RATE", "GOAL", "STATUS", "DURATION"],
                    [[
                        "purge",
                        output.Source,
                        output.MessagesProcessed.ToString("N0"),
                        output.MessagesPurged.ToString("N0"),
                        output.MessagesFailed.ToString("N0"),
                        $"{output.AverageMessagesPerSecond:N1}/s",
                        output.Goal,
                        output.Status,
                        OutputWriter.FormatDuration(output.ElapsedTime)
                    ]]);
                break;
        }
    }

    private static void RenderSchedule(ScheduleCommandOutput output)
    {
        switch (output.RequestedFormat)
        {
            case OutputFormat.Json:
                OutputWriter.PrintJsonEnvelope(
                    command: "schedule",
                    resource: new { source = output.Source },
                    startedAt: output.StartedAt,
                    completedAt: output.CompletedAt,
                    status: output.Status,
                    summary: new
                    {
                        processed = output.MessagesProcessed,
                        scheduled = output.MessagesScheduled,
                        failed = output.MessagesFailed,
                        averageRate = output.AverageMessagesPerSecond,
                        goal = output.Goal,
                        goalReached = output.GoalReached
                    },
                    details: new
                    {
                        authentication = output.Authentication,
                        selection = output.Selection,
                        messageId = output.MessageId,
                        scheduleAtUtc = output.ScheduleAtUtc,
                        execution = new
                        {
                            receiveBatchSize = output.ReceiveBatchSize,
                            prefetch = output.PrefetchCount
                        },
                        behavior = new
                        {
                            continueOnError = output.ContinueOnError
                        }
                    });
                break;
            case OutputFormat.Tsv:
                OutputWriter.PrintTsv(
                    ["NAME", "SOURCE", "PROCESSED", "SCHEDULED", "FAILED", "RATE", "GOAL", "STATUS", "DURATION"],
                    [[
                        "schedule",
                        output.Source,
                        output.MessagesProcessed.ToString("N0"),
                        output.MessagesScheduled.ToString("N0"),
                        output.MessagesFailed.ToString("N0"),
                        $"{output.AverageMessagesPerSecond:N1}/s",
                        output.Goal,
                        output.Status,
                        OutputWriter.FormatDuration(output.ElapsedTime)
                    ]]);
                break;
        }
    }

    private static void RenderExpedite(ExpediteCommandOutput output)
    {
        switch (output.RequestedFormat)
        {
            case OutputFormat.Json:
                OutputWriter.PrintJsonEnvelope(
                    command: "expedite",
                    resource: new { source = output.Source },
                    startedAt: output.StartedAt,
                    completedAt: output.CompletedAt,
                    status: output.Status,
                    summary: new
                    {
                        processed = output.MessagesProcessed,
                        expedited = output.MessagesExpedited,
                        failed = output.MessagesFailed,
                        averageRate = output.AverageMessagesPerSecond,
                        goal = output.Goal,
                        goalReached = output.GoalReached
                    },
                    details: new
                    {
                        authentication = output.Authentication,
                        selection = output.Selection,
                        messageId = output.MessageId,
                        execution = new
                        {
                            peekBatchSize = output.PeekBatchSize,
                            prefetch = output.PrefetchCount
                        },
                        behavior = new
                        {
                            continueOnError = output.ContinueOnError
                        }
                    });
                break;
            case OutputFormat.Tsv:
                OutputWriter.PrintTsv(
                    ["NAME", "SOURCE", "PROCESSED", "EXPEDITED", "FAILED", "RATE", "GOAL", "STATUS", "DURATION"],
                    [[
                        "expedite",
                        output.Source,
                        output.MessagesProcessed.ToString("N0"),
                        output.MessagesExpedited.ToString("N0"),
                        output.MessagesFailed.ToString("N0"),
                        $"{output.AverageMessagesPerSecond:N1}/s",
                        output.Goal,
                        output.Status,
                        OutputWriter.FormatDuration(output.ElapsedTime)
                    ]]);
                break;
        }
    }

    private static void RenderList(ListCommandOutput output)
    {
        switch (output.RequestedFormat)
        {
            case OutputFormat.Json:
                var now = DateTime.Now;
                OutputWriter.PrintJsonEnvelope(
                    command: "list",
                    resource: new
                    {
                        kind = output.Kind.ToString().ToLowerInvariant(),
                        topic = output.TopicName
                    },
                    startedAt: now,
                    completedAt: now,
                    status: "completed",
                    summary: new { count = ResolveListCount(output) },
                    details: new
                    {
                        authentication = output.Authentication,
                        items = BuildListJsonItems(output)
                    });
                break;
            case OutputFormat.Tsv:
                switch (output.Kind)
                {
                    case ListEntityKind.Queues:
                        PrintQueuesTsv(output.Queues);
                        break;
                    case ListEntityKind.Topics:
                        PrintTopicsTsv(output.Topics);
                        break;
                    case ListEntityKind.Subscriptions:
                        PrintSubscriptionsTsv(output.Subscriptions);
                        break;
                }
                break;
        }
    }

    private static void RenderInspect(InspectCommandOutput output)
    {
        switch (output.Kind)
        {
            case InspectedEntityKind.Queue:
                RenderInspectQueue(output);
                break;
            case InspectedEntityKind.Topic:
                RenderInspectTopic(output);
                break;
            case InspectedEntityKind.Subscription:
                RenderInspectSubscription(output);
                break;
            default:
                OutputWriter.PrintError($"Entity '{output.Path}' was not found as a queue, topic, or subscription.");
                break;
        }
    }

    private static void RenderInspectQueue(InspectCommandOutput output)
    {
        var props = output.QueueProperties!;
        var runtime = output.QueueRuntimeProperties!;
        if (output.RequestedFormat == OutputFormat.Json)
        {
            var now = DateTime.Now;
            OutputWriter.PrintJsonEnvelope(
                command: "inspect",
                resource: new { path = output.Path, entityType = "queue" },
                startedAt: now, completedAt: now, status: "completed",
                summary: new
                {
                    status = props.Status.ToString(),
                    total = runtime.TotalMessageCount,
                    active = runtime.ActiveMessageCount,
                    deadLetter = runtime.DeadLetterMessageCount,
                    scheduled = runtime.ScheduledMessageCount
                },
                details: new
                {
                    authentication = output.Authentication,
                    properties = new
                    {
                        props.MaxSizeInMegabytes,
                        LockDuration = props.LockDuration.ToString(),
                        props.MaxDeliveryCount,
                        DefaultMessageTimeToLive = props.DefaultMessageTimeToLive.ToString(),
                        AutoDeleteOnIdle = props.AutoDeleteOnIdle.ToString(),
                        props.RequiresDuplicateDetection,
                        DuplicateDetectionHistoryTimeWindow = props.DuplicateDetectionHistoryTimeWindow.ToString(),
                        props.RequiresSession,
                        props.DeadLetteringOnMessageExpiration,
                        props.EnablePartitioning,
                        props.EnableBatchedOperations,
                        props.ForwardTo,
                        props.ForwardDeadLetteredMessagesTo,
                        props.UserMetadata
                    },
                    runtime = new
                    {
                        runtime.TotalMessageCount, runtime.ActiveMessageCount, runtime.DeadLetterMessageCount,
                        runtime.ScheduledMessageCount, runtime.TransferMessageCount, runtime.TransferDeadLetterMessageCount,
                        runtime.SizeInBytes,
                        CreatedAt = runtime.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        UpdatedAt = runtime.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        AccessedAt = runtime.AccessedAt.ToString("yyyy-MM-ddTHH:mm:ssK")
                    }
                });
            return;
        }

        var rows = BuildInspectConsolidatedTableRows(
            "queue", output.Path, props.Status.ToString(),
            [
                ("TOTAL", runtime.TotalMessageCount.ToString("N0")),
                ("ACTIVE", runtime.ActiveMessageCount.ToString("N0")),
                ("DLQ", runtime.DeadLetterMessageCount.ToString("N0")),
                ("SCHEDULED", runtime.ScheduledMessageCount.ToString("N0"))
            ],
            [
                ("AUTH", output.Authentication),
                ("MAX_SIZE_MB", props.MaxSizeInMegabytes.ToString("N0")),
                ("LOCK_DURATION", props.LockDuration.ToString(@"hh\:mm\:ss")),
                ("MAX_DELIVERY", props.MaxDeliveryCount.ToString("N0")),
                ("DEFAULT_TTL", props.DefaultMessageTimeToLive.ToString(@"dd\.hh\:mm\:ss")),
                ("AUTO_DELETE_IDLE", props.AutoDeleteOnIdle.ToString(@"dd\.hh\:mm\:ss")),
                ("DUPLICATE_DETECTION", props.RequiresDuplicateDetection.ToString()),
                ("DUPLICATE_WINDOW", props.DuplicateDetectionHistoryTimeWindow.ToString(@"hh\:mm\:ss")),
                ("REQUIRES_SESSION", props.RequiresSession.ToString()),
                ("DLQ_ON_EXPIRY", props.DeadLetteringOnMessageExpiration.ToString()),
                ("PARTITIONING", props.EnablePartitioning.ToString()),
                ("BATCHED_OPS", props.EnableBatchedOperations.ToString()),
                ("FORWARD_TO", FormatInspectValue(props.ForwardTo)),
                ("FORWARD_DLQ_TO", FormatInspectValue(props.ForwardDeadLetteredMessagesTo)),
                ("USER_METADATA", FormatInspectValue(props.UserMetadata)),
                ("TRANSFER", runtime.TransferMessageCount.ToString("N0")),
                ("TRANSFER_DLQ", runtime.TransferDeadLetterMessageCount.ToString("N0")),
                ("SIZE_BYTES", runtime.SizeInBytes.ToString("N0"))
            ]);
        PrintInspectRows(rows, output.RequestedFormat);
    }

    private static void RenderInspectTopic(InspectCommandOutput output)
    {
        var props = output.TopicProperties!;
        var runtime = output.TopicRuntimeProperties!;
        if (output.RequestedFormat == OutputFormat.Json)
        {
            var now = DateTime.Now;
            OutputWriter.PrintJsonEnvelope(
                command: "inspect",
                resource: new { path = output.Path, entityType = "topic" },
                startedAt: now, completedAt: now, status: "completed",
                summary: new
                {
                    status = props.Status.ToString(),
                    subscriptions = runtime.SubscriptionCount,
                    scheduled = runtime.ScheduledMessageCount,
                    sizeInBytes = runtime.SizeInBytes
                },
                details: new
                {
                    authentication = output.Authentication,
                    properties = new
                    {
                        props.MaxSizeInMegabytes,
                        DefaultMessageTimeToLive = props.DefaultMessageTimeToLive.ToString(),
                        AutoDeleteOnIdle = props.AutoDeleteOnIdle.ToString(),
                        props.RequiresDuplicateDetection,
                        DuplicateDetectionHistoryTimeWindow = props.DuplicateDetectionHistoryTimeWindow.ToString(),
                        props.SupportOrdering,
                        props.EnablePartitioning,
                        props.EnableBatchedOperations,
                        props.UserMetadata
                    },
                    runtime = new
                    {
                        runtime.SubscriptionCount, runtime.ScheduledMessageCount, runtime.SizeInBytes,
                        CreatedAt = runtime.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        UpdatedAt = runtime.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        AccessedAt = runtime.AccessedAt.ToString("yyyy-MM-ddTHH:mm:ssK")
                    }
                });
            return;
        }

        var rows = BuildInspectConsolidatedTableRows(
            "topic", output.Path, props.Status.ToString(),
            [
                ("SUBSCRIPTIONS", runtime.SubscriptionCount.ToString("N0")),
                ("SCHEDULED", runtime.ScheduledMessageCount.ToString("N0"))
            ],
            [
                ("AUTH", output.Authentication),
                ("MAX_SIZE_MB", props.MaxSizeInMegabytes.ToString("N0")),
                ("DEFAULT_TTL", props.DefaultMessageTimeToLive.ToString(@"dd\.hh\:mm\:ss")),
                ("AUTO_DELETE_IDLE", props.AutoDeleteOnIdle.ToString(@"dd\.hh\:mm\:ss")),
                ("DUPLICATE_DETECTION", props.RequiresDuplicateDetection.ToString()),
                ("DUPLICATE_WINDOW", props.DuplicateDetectionHistoryTimeWindow.ToString(@"hh\:mm\:ss")),
                ("SUPPORT_ORDERING", props.SupportOrdering.ToString()),
                ("PARTITIONING", props.EnablePartitioning.ToString()),
                ("BATCHED_OPS", props.EnableBatchedOperations.ToString()),
                ("USER_METADATA", FormatInspectValue(props.UserMetadata)),
                ("SIZE_BYTES", runtime.SizeInBytes.ToString("N0"))
            ]);
        PrintInspectRows(rows, output.RequestedFormat);
    }

    private static void RenderInspectSubscription(InspectCommandOutput output)
    {
        var props = output.SubscriptionProperties!;
        var runtime = output.SubscriptionRuntimeProperties!;
        if (output.RequestedFormat == OutputFormat.Json)
        {
            var now = DateTime.Now;
            OutputWriter.PrintJsonEnvelope(
                command: "inspect",
                resource: new { path = output.Path, entityType = "subscription" },
                startedAt: now, completedAt: now, status: "completed",
                summary: new
                {
                    status = props.Status.ToString(),
                    total = runtime.TotalMessageCount,
                    active = runtime.ActiveMessageCount,
                    deadLetter = runtime.DeadLetterMessageCount
                },
                details: new
                {
                    authentication = output.Authentication,
                    properties = new
                    {
                        LockDuration = props.LockDuration.ToString(),
                        props.MaxDeliveryCount,
                        DefaultMessageTimeToLive = props.DefaultMessageTimeToLive.ToString(),
                        AutoDeleteOnIdle = props.AutoDeleteOnIdle.ToString(),
                        props.RequiresSession,
                        props.DeadLetteringOnMessageExpiration,
                        props.EnableDeadLetteringOnFilterEvaluationExceptions,
                        props.EnableBatchedOperations,
                        props.ForwardTo,
                        props.ForwardDeadLetteredMessagesTo,
                        props.UserMetadata
                    },
                    runtime = new
                    {
                        runtime.TotalMessageCount, runtime.ActiveMessageCount, runtime.DeadLetterMessageCount,
                        runtime.TransferMessageCount, runtime.TransferDeadLetterMessageCount,
                        CreatedAt = runtime.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        UpdatedAt = runtime.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssK"),
                        AccessedAt = runtime.AccessedAt.ToString("yyyy-MM-ddTHH:mm:ssK")
                    }
                });
            return;
        }

        var rows = BuildInspectConsolidatedTableRows(
            "subscription", output.Path, props.Status.ToString(),
            [
                ("TOTAL", runtime.TotalMessageCount.ToString("N0")),
                ("ACTIVE", runtime.ActiveMessageCount.ToString("N0")),
                ("DLQ", runtime.DeadLetterMessageCount.ToString("N0"))
            ],
            [
                ("AUTH", output.Authentication),
                ("LOCK_DURATION", props.LockDuration.ToString(@"hh\:mm\:ss")),
                ("MAX_DELIVERY", props.MaxDeliveryCount.ToString("N0")),
                ("DEFAULT_TTL", props.DefaultMessageTimeToLive.ToString(@"dd\.hh\:mm\:ss")),
                ("AUTO_DELETE_IDLE", props.AutoDeleteOnIdle.ToString(@"dd\.hh\:mm\:ss")),
                ("REQUIRES_SESSION", props.RequiresSession.ToString()),
                ("DLQ_ON_EXPIRY", props.DeadLetteringOnMessageExpiration.ToString()),
                ("DLQ_ON_FILTER_ERRORS", props.EnableDeadLetteringOnFilterEvaluationExceptions.ToString()),
                ("BATCHED_OPS", props.EnableBatchedOperations.ToString()),
                ("FORWARD_TO", FormatInspectValue(props.ForwardTo)),
                ("FORWARD_DLQ_TO", FormatInspectValue(props.ForwardDeadLetteredMessagesTo)),
                ("USER_METADATA", FormatInspectValue(props.UserMetadata)),
                ("TRANSFER", runtime.TransferMessageCount.ToString("N0")),
                ("TRANSFER_DLQ", runtime.TransferDeadLetterMessageCount.ToString("N0"))
            ]);
        PrintInspectRows(rows, output.RequestedFormat);
    }

    private static void PrintQueuesTsv(IReadOnlyList<QueueListEntry> queues)
    {
        var rows = queues.Select(q => new[]
        {
            "queue", q.Name, q.Status.ToString(),
            q.ActiveMessageCount.ToString("N0"), q.DeadLetterMessageCount.ToString("N0"),
            q.ScheduledMessageCount.ToString("N0"), q.TotalMessageCount.ToString("N0"),
            q.SizeInBytes.ToString("N0"), q.MaxSizeInMegabytes.ToString("N0"),
            FormatFreeSpace(q.SizeInBytes, q.MaxSizeInMegabytes)
        }).ToList();
        OutputWriter.PrintTsv(
            ["TYPE", "NAME", "STATUS", "ACTIVE", "DLQ", "SCHEDULED", "TOTAL", "SIZE_BYTES", "MAX_MB", "FREE_SPACE"],
            rows);
    }

    private static void PrintTopicsTsv(IReadOnlyList<TopicListEntry> topics)
    {
        var rows = topics.Select(t => new[]
        {
            "topic", t.Name, t.Status.ToString(),
            t.SubscriptionCount.ToString("N0"), t.ScheduledMessageCount.ToString("N0"),
            t.SizeInBytes.ToString("N0"), t.MaxSizeInMegabytes.ToString("N0"),
            FormatFreeSpace(t.SizeInBytes, t.MaxSizeInMegabytes)
        }).ToList();
        OutputWriter.PrintTsv(
            ["TYPE", "NAME", "STATUS", "SUBSCRIPTIONS", "SCHEDULED", "SIZE_BYTES", "MAX_MB", "FREE_SPACE"],
            rows);
    }

    private static void PrintSubscriptionsTsv(IReadOnlyList<SubscriptionListEntry> subscriptions)
    {
        var rows = subscriptions.Select(s => new[]
        {
            "subscription", s.Name, s.Status.ToString(),
            s.ActiveMessageCount.ToString("N0"), s.DeadLetterMessageCount.ToString("N0"),
            s.TransferDeadLetterMessageCount.ToString("N0"), s.TotalMessageCount.ToString("N0"),
            s.MaxDeliveryCount.ToString("N0"), s.LockDuration.ToString(@"hh\:mm\:ss")
        }).ToList();
        OutputWriter.PrintTsv(
            ["TYPE", "NAME", "STATUS", "ACTIVE", "DLQ", "TRANSFER_DLQ", "TOTAL", "MAX_DELIVERY", "LOCK_DURATION"],
            rows);
    }

    private static int ResolveListCount(ListCommandOutput output)
        => output.Kind switch
        {
            ListEntityKind.Queues => output.Queues.Count,
            ListEntityKind.Topics => output.Topics.Count,
            ListEntityKind.Subscriptions => output.Subscriptions.Count,
            _ => 0
        };

    private static object BuildListJsonItems(ListCommandOutput output)
        => output.Kind switch
        {
            ListEntityKind.Queues => output.Queues.Select(q => new
            {
                type = "queue", q.Name, Status = q.Status.ToString(),
                q.ActiveMessageCount, q.DeadLetterMessageCount, q.ScheduledMessageCount,
                q.TotalMessageCount, q.SizeInBytes, q.MaxSizeInMegabytes,
                FreeSpacePercent = ComputeFreeSpacePercent(q.SizeInBytes, q.MaxSizeInMegabytes)
            }).ToList<object>(),
            ListEntityKind.Topics => output.Topics.Select(t => new
            {
                type = "topic", t.Name, Status = t.Status.ToString(),
                t.SubscriptionCount, t.ScheduledMessageCount, t.SizeInBytes, t.MaxSizeInMegabytes,
                FreeSpacePercent = ComputeFreeSpacePercent(t.SizeInBytes, t.MaxSizeInMegabytes)
            }).ToList<object>(),
            ListEntityKind.Subscriptions => output.Subscriptions.Select(s => new
            {
                type = "subscription", s.Name, Status = s.Status.ToString(),
                s.ActiveMessageCount, s.DeadLetterMessageCount, s.TransferDeadLetterMessageCount,
                s.TotalMessageCount, s.MaxDeliveryCount, LockDuration = s.LockDuration.ToString()
            }).ToList<object>(),
            _ => new object()
        };

    private static string FormatFreeSpace(long sizeInBytes, long maxSizeInMegabytes)
    {
        var percent = ComputeFreeSpacePercent(sizeInBytes, maxSizeInMegabytes);
        return percent.HasValue ? $"{percent.Value:F1}%" : "N/A";
    }

    private static double? ComputeFreeSpacePercent(long sizeInBytes, long maxSizeInMegabytes)
    {
        if (maxSizeInMegabytes <= 0) return null;
        var maxBytes = maxSizeInMegabytes * 1024L * 1024L;
        var freeBytes = maxBytes - sizeInBytes;
        return Math.Max(0.0, freeBytes * 100.0 / maxBytes);
    }

    private static void PrintInspectRows(List<(string Field, string Value)> rows, OutputFormat format)
    {
        if (format != OutputFormat.Tsv)
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format.");
        }

        foreach (var line in BuildInspectTsvLines(rows))
            Console.WriteLine(line);
    }

    public static List<(string Field, string Value)> BuildInspectConsolidatedTableRows(
        string entityType,
        string path,
        string status,
        IReadOnlyList<(string Field, string Value)> summaryRows,
        IReadOnlyList<(string Field, string Value)> detailRows)
    {
        var rows = new List<(string Field, string Value)>
        {
            ("NAME", "inspect"),
            ("TYPE", entityType),
            ("PATH", path),
            ("STATUS", status)
        };
        rows.AddRange(summaryRows);
        rows.AddRange(detailRows);
        return rows;
    }

    public static List<string> BuildInspectTsvLines(IReadOnlyList<(string Field, string Value)> rows)
        => [
            string.Join('\t', rows.Select(r => r.Field)),
            string.Join('\t', rows.Select(r => r.Value))
        ];

    private static string FormatInspectValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "(none)" : value;
}
