namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class CommandLineParsing
{
    public static List<string> StripLeadingCommandToken(List<string> positional, string commandName)
    {
        if (positional.Count == 0)
        {
            return positional;
        }

        if (string.Equals(positional[0], commandName, StringComparison.OrdinalIgnoreCase))
        {
            return positional.Skip(1).ToList();
        }

        return positional;
    }

    public static string GetNextValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {argumentName}.");
        }

        return args[++index];
    }

    public static int ParsePositiveInt(string value, string label)
    {
        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Invalid {label} value: Must be a number.");
        }

        if (parsed <= 0)
        {
            throw new ArgumentException($"Invalid {label} value: Must be a positive number.");
        }

        return parsed;
    }

    public static int ParseNonNegativeInt(string value, string label)
    {
        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Invalid {label} value: Must be a number.");
        }

        if (parsed < 0)
        {
            throw new ArgumentException($"Invalid {label} value: Must be zero or greater.");
        }

        return parsed;
    }

    public static string NormalizeNamespace(string value)
    {
        var normalized = value.Trim();

        if (normalized.StartsWith("sb://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["sb://".Length..];
        }

        normalized = normalized.TrimEnd('/');

        if (normalized.Contains('/'))
        {
            throw new ArgumentException("Service Bus namespace must not include entity path segments.");
        }

        if (!normalized.Contains('.'))
        {
            normalized = $"{normalized}.servicebus.windows.net";
        }

        if (!normalized.EndsWith(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Service Bus namespace must be a valid namespace host, e.g. '<name>.servicebus.windows.net'.");
        }

        return normalized;
    }

    public static SourceEndpoint ParseSourcePath(string? path, string missingSourceMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(missingSourceMessage);
        }

        var parsed = ParseEntityPath(path);
        return parsed.kind switch
        {
            SourceEntityKind.Queue => new SourceEndpoint(SourceEntityKind.Queue, parsed.entityName, null, parsed.subQueue),
            SourceEntityKind.Subscription => new SourceEndpoint(SourceEntityKind.Subscription, parsed.entityName, parsed.subscriptionName, parsed.subQueue),
            _ => throw new ArgumentException($"Unsupported source path: {path}")
        };
    }

    public static string ParseTargetEntityPath(string? path, string missingTargetMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(missingTargetMessage);
        }

        var parsed = ParseEntityPath(path);
        if (parsed.subQueue != SourceSubQueue.None)
        {
            throw new ArgumentException($"Invalid target path '{path}': target cannot be a subqueue path.");
        }

        if (parsed.kind == SourceEntityKind.Subscription)
        {
            throw new ArgumentException($"Invalid target path '{path}': target cannot be a subscription path.");
        }

        return parsed.entityName;
    }

    private static (SourceEntityKind kind, string entityName, string? subscriptionName, SourceSubQueue subQueue) ParseEntityPath(string path)
    {
        var trimmedPath = path.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            throw new ArgumentException("Entity path cannot be empty.");
        }

        if (trimmedPath.Contains(':'))
        {
            throw new ArgumentException($"Invalid Azure Service Bus path '{path}'. Use native entity paths only.");
        }

        var (basePath, subQueue) = ParseSubQueueSuffix(trimmedPath);
        var segments = basePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 1)
        {
            return (SourceEntityKind.Queue, segments[0], null, subQueue);
        }

        if (segments.Length == 3 && segments[1].Equals("Subscriptions", StringComparison.OrdinalIgnoreCase))
        {
            return (SourceEntityKind.Subscription, segments[0], segments[2], subQueue);
        }

        throw new ArgumentException($"Invalid Azure Service Bus path '{path}'. Use '<queue>' or '<topic>/Subscriptions/<subscription>' with optional subqueue suffix.");
    }

    private static (string basePath, SourceSubQueue subQueue) ParseSubQueueSuffix(string path)
    {
        const string deadLetterSuffix = "/$DeadLetterQueue";
        const string transferDeadLetterSuffix = "/$Transfer/$DeadLetterQueue";

        if (path.EndsWith(transferDeadLetterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return (path[..^transferDeadLetterSuffix.Length], SourceSubQueue.TransferDeadLetter);
        }

        if (path.EndsWith(deadLetterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return (path[..^deadLetterSuffix.Length], SourceSubQueue.DeadLetter);
        }

        return (path, SourceSubQueue.None);
    }
}
