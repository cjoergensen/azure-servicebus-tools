using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class OutputWriter
{
    private sealed record CommandEnvelope(
        string Name,
        string StartedAt,
        string CompletedAt,
        string Duration,
        double DurationSeconds,
        string Status);

    private sealed record JsonEnvelope(
        CommandEnvelope Command,
        object Resource,
        object? Summary,
        object? Details,
        IReadOnlyList<string>? Warnings);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void PrintTable(string[] headers, IReadOnlyList<string[]> rows)
    {
        if (headers.Length == 0)
        {
            return;
        }

        var widths = new int[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        for (int i = 0; i < headers.Length; i++)
        {
            Console.ResetColor();
            Console.Write(headers[i].PadRight(widths[i]));
            if (i < headers.Length - 1)
            {
                Console.Write("  ");
            }
        }

        Console.WriteLine();

        for (int i = 0; i < headers.Length; i++)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('-', widths[i]));
            if (i < headers.Length - 1)
            {
                Console.Write("  ");
            }
        }

        Console.WriteLine();

        foreach (var row in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                Console.ResetColor();
                Console.Write(row[i].PadRight(widths[i]));
                if (i < headers.Length - 1)
                {
                    Console.Write("  ");
                }
            }

            Console.WriteLine();
        }

        Console.ResetColor();
    }

    public static string FormatTimestamp(DateTime timestamp)
        => timestamp.ToString("yyyy-MM-ddTHH:mm:ssK");

    public static string FormatDuration(TimeSpan duration)
        => duration.ToString(@"hh\:mm\:ss");

    public static string ResolveStatus(int failedCount)
        => failedCount > 0 ? "completed_with_failures" : "completed";

    public static void PrintJsonEnvelope(
        string command,
        object resource,
        DateTime startedAt,
        DateTime completedAt,
        string status,
        object? summary,
        object? details,
        IReadOnlyList<string>? warnings = null)
    {
        var duration = completedAt - startedAt;
        var payload = new JsonEnvelope(
            Command: new CommandEnvelope(
                Name: command,
                StartedAt: FormatTimestamp(startedAt),
                CompletedAt: FormatTimestamp(completedAt),
                Duration: FormatDuration(duration),
                DurationSeconds: Math.Round(duration.TotalSeconds, 1),
                Status: status),
            Resource: resource,
            Summary: summary,
            Details: details,
            Warnings: warnings is { Count: > 0 } ? warnings : null);

        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static void PrintTsv(string[] headers, IReadOnlyList<string[]> rows)
    {
        Console.WriteLine(string.Join('\t', headers));
        foreach (var row in rows)
            Console.WriteLine(string.Join('\t', row));
    }

    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {message}");
        Console.ResetColor();
    }

    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Warning: {message}");
        Console.ResetColor();
    }
}
