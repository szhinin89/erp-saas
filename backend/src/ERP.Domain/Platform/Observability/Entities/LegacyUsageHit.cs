namespace ERP.Domain.Platform.Observability.Entities;

/// <summary>Hit reciente append-only para auditoría strangler.</summary>
public sealed class LegacyUsageHit
{
    public Guid Id { get; private set; }
    public LegacyUsageCategory Category { get; private set; }
    public string UsageKey { get; private set; } = null!;
    public string? Successor { get; private set; }
    public DateTimeOffset HitAtUtc { get; private set; }
    public string? CallerIp { get; private set; }
    public string? Detail { get; private set; }

    private LegacyUsageHit() { }

    public static LegacyUsageHit Create(
        LegacyUsageCategory category,
        string usageKey,
        string? successor,
        string? callerIp,
        string? detail)
        => new()
        {
            Id = Guid.NewGuid(),
            Category = category,
            UsageKey = usageKey.Trim(),
            Successor = successor,
            HitAtUtc = DateTimeOffset.UtcNow,
            CallerIp = callerIp,
            Detail = detail,
        };
}
