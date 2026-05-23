namespace ERP.Domain.Platform.Observability.Entities;

/// <summary>Contadores agregados de uso legacy (strangler telemetry — Phase 3 persistente).</summary>
public sealed class LegacyUsageStat
{
    public const int UsageKeyMaxLen = 512;
    public const int SuccessorMaxLen = 512;
    public const int CallerIpMaxLen = 45;
    public const int DetailMaxLen = 256;

    public Guid Id { get; private set; }
    public LegacyUsageCategory Category { get; private set; }
    public string UsageKey { get; private set; } = null!;
    public string? Successor { get; private set; }
    public long TotalHits { get; private set; }
    public DateTimeOffset LastHitUtc { get; private set; }
    public string? LastCallerIp { get; private set; }
    public string? LastDetail { get; private set; }

    private LegacyUsageStat() { }

    public static LegacyUsageStat Create(
        LegacyUsageCategory category,
        string usageKey,
        string? successor,
        string? callerIp,
        string? detail)
    {
        if (string.IsNullOrWhiteSpace(usageKey))
            throw new ArgumentException("UsageKey requerida.", nameof(usageKey));

        return new LegacyUsageStat
        {
            Id = Guid.NewGuid(),
            Category = category,
            UsageKey = Truncate(usageKey, UsageKeyMaxLen)!,
            Successor = Truncate(successor, SuccessorMaxLen),
            TotalHits = 1,
            LastHitUtc = DateTimeOffset.UtcNow,
            LastCallerIp = Truncate(callerIp, CallerIpMaxLen),
            LastDetail = Truncate(detail, DetailMaxLen),
        };
    }

    public void RecordHit(string? callerIp, string? detail, string? successor)
    {
        TotalHits++;
        LastHitUtc = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(callerIp))
            LastCallerIp = Truncate(callerIp, CallerIpMaxLen);
        if (!string.IsNullOrWhiteSpace(detail))
            LastDetail = Truncate(detail, DetailMaxLen);
        if (!string.IsNullOrWhiteSpace(successor))
            Successor = Truncate(successor, SuccessorMaxLen);
    }

    private static string? Truncate(string? value, int maxLen) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLen)];
}
