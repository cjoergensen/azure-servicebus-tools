namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class CommandLineValidation
{
    public static List<string> ResolveCommandOperands(List<string> positional, string commandName)
    {
        return CommandLineParsing.StripLeadingCommandToken(positional, commandName);
    }

    public static void EnsureExpectedOperandCount(IReadOnlyList<string> tokens, int expectedCount, string usageError)
    {
        if (tokens.Count != expectedCount)
        {
            throw new ArgumentException(usageError);
        }
    }

    public static void EnsureNotMixedWithOptionValue(bool hasOptionValue, string mixedModeError)
    {
        if (hasOptionValue)
        {
            throw new ArgumentException(mixedModeError);
        }
    }

    public static void ValidateAuthentication(string? connectionString, string? serviceBusNamespace)
    {
        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var hasNamespace = !string.IsNullOrWhiteSpace(serviceBusNamespace);

        if (!hasConnectionString && !hasNamespace)
        {
            throw new ArgumentException("Authentication is required: provide either --connection-string or --namespace.");
        }

        if (hasConnectionString && hasNamespace)
        {
            throw new ArgumentException("Provide either --connection-string or --namespace, not both.");
        }

        if (hasConnectionString &&
            (!connectionString!.Contains("Endpoint=") || !connectionString.Contains("SharedAccessKey=")))
        {
            throw new ArgumentException("Invalid Service Bus connection string format.");
        }
    }

    public static void ValidateSourcePathCompatibility(SourceEndpoint source)
    {
        if (source.Kind == SourceEntityKind.Subscription &&
            source.SubQueue == SourceSubQueue.TransferDeadLetter)
        {
            throw new ArgumentException("Transfer dead-letter subqueue is only supported for queue sources.");
        }
    }

    public static void ValidateWorkerSettings(
        int maxConcurrency,
        int maxMessagesPerBatch,
        int prefetchCount,
        int? maxMessages)
    {
        if (maxConcurrency <= 0)
        {
            throw new ArgumentException("Max concurrency must be a positive number.");
        }

        if (maxConcurrency > SharedConstants.MaxAllowedConcurrency)
        {
            throw new ArgumentException($"Max concurrency cannot exceed {SharedConstants.MaxAllowedConcurrency}.");
        }

        if (maxMessagesPerBatch <= 0)
        {
            throw new ArgumentException("Max messages per batch must be a positive number.");
        }

        if (maxMessagesPerBatch > SharedConstants.MaxAllowedMessagesPerBatch)
        {
            throw new ArgumentException($"Max messages per batch cannot exceed {SharedConstants.MaxAllowedMessagesPerBatch}.");
        }

        if (prefetchCount < 0)
        {
            throw new ArgumentException("Prefetch count must be zero or greater.");
        }

        if (maxMessages is <= 0)
        {
            throw new ArgumentException("Max messages must be a positive number.");
        }
    }
}
