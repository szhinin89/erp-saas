using ERP.Domain.Platform.Observability.Entities;

namespace ERP.Application.Platform.Observability;

public interface ILegacyUsageTelemetryStore
{
    Task RecordAsync(
        LegacyUsageCategory category,
        string usageKey,
        string? successor,
        string? callerIp,
        string? detail,
        CancellationToken ct = default);

    Task<LegacyStranglerDashboard> GetDashboardAsync(int top, int recent, CancellationToken ct = default);
}
