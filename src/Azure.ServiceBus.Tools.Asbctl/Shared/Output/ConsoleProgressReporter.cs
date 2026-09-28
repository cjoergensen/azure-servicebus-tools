namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public sealed class ConsoleProgressReporter(
    DateTime startTime,
    string successLabel,
    Func<int?> goalProvider)
{
    private int lastProgressLineLength;

    public void Print(int succeeded, int failed)
    {
        var elapsed = DateTime.Now - startTime;
        var processed = succeeded + failed;
        var messagesPerSecond = elapsed.TotalSeconds > 0
            ? Math.Round(processed / elapsed.TotalSeconds, 1)
            : 0;
        var goal = goalProvider();
        var remaining = goal.HasValue ? Math.Max(0, goal.Value - processed) : (int?)null;
        var progressPercent = goal.HasValue && goal.Value > 0
            ? processed * 100.0 / goal.Value
            : (double?)null;
        var successRate = processed > 0 ? succeeded * 100.0 / processed : 100.0;
        var estimatedSecondsRemaining = remaining.HasValue && messagesPerSecond > 0
            ? remaining.Value / messagesPerSecond
            : (double?)null;
        var etaText = estimatedSecondsRemaining.HasValue
            ? TimeSpan.FromSeconds(estimatedSecondsRemaining.Value).ToString(@"hh\:mm\:ss")
            : "--:--:--";
        var progressText =
            $"Processed {processed:N0} ({successLabel} {succeeded:N0}, failed {failed:N0}) | " +
            $"rate {messagesPerSecond:N1}/s | success {successRate:N1}% | " +
            $"goal {(goal.HasValue ? goal.Value.ToString("N0") : "unlimited")} " +
            $"{(progressPercent.HasValue ? $"({Math.Min(100.0, progressPercent.Value):N1}%)" : string.Empty)} | " +
            $"remaining {(remaining.HasValue ? remaining.Value.ToString("N0") : "N/A")} | " +
            $"elapsed {elapsed:hh\\:mm\\:ss} | eta {etaText}";

        Console.Write("\r");
        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        else
        {
            Console.ResetColor();
        }
        Console.Write(progressText);
        if (progressText.Length < lastProgressLineLength)
        {
            Console.Write(new string(' ', lastProgressLineLength - progressText.Length));
        }

        lastProgressLineLength = progressText.Length;
        Console.ResetColor();
    }

    public static int? ResolveGoal(int? maxMessages, long? sourceCount)
    {
        if (maxMessages.HasValue)
        {
            return maxMessages.Value;
        }

        if (sourceCount.HasValue && sourceCount.Value <= int.MaxValue)
        {
            return (int)sourceCount.Value;
        }

        return null;
    }
}
