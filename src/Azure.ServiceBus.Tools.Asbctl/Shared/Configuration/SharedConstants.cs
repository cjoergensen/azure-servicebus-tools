namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class SharedConstants
{
    public const int DefaultMaxConcurrency = 8;
    public const int DefaultMaxMessagesPerBatch = 25;
    public const int MaxAllowedMessagesPerBatch = 500;
    public const int MaxAllowedConcurrency = 32;
    public const int DefaultMaxIdleMinutes = 2;
    public const int DefaultReceiveWaitSeconds = 2;
    public const int ConsecutiveEmptyReceivesBeforeStop = 10;
    public const int ConsecutiveEmptyReceivesBeforeStopWhenLimited = 30;
    public const int EmptyReceiveBackoffMilliseconds = 250;
}
