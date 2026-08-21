namespace ZH.PrintAgent.Core;

public sealed record PrintProcessingOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan ProcessingStaleAfter { get; init; } = TimeSpan.FromMinutes(5);
}
