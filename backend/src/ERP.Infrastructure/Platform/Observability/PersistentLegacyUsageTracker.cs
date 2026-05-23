using ERP.Application.Platform.Observability;
using ERP.Domain.Platform.Observability.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Platform.Observability;

/// <summary>Phase 3 — enqueues legacy telemetry for PostgreSQL persistence via background worker.</summary>
public sealed class PersistentLegacyUsageTracker : ILegacyEndpointUsageTracker
{
    private readonly ILegacyUsageTelemetryChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;

    public PersistentLegacyUsageTracker(
        ILegacyUsageTelemetryChannel channel,
        IServiceScopeFactory scopeFactory)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
    }

    public void Record(string endpoint, string method, string? successor, string? callerIp)
    {
        var key = $"{method.Trim().ToUpperInvariant()} {endpoint.Trim()}";
        _channel.Enqueue(new LegacyUsageRecord(
            LegacyUsageCategory.Endpoint,
            key,
            successor,
            callerIp,
            null));
    }

    public void RecordUiRoute(string routeKey, string? referrer)
        => _channel.Enqueue(new LegacyUsageRecord(
            LegacyUsageCategory.UiRoute,
            routeKey.Trim(),
            null,
            null,
            referrer));

    public void RecordMasterData(string sourceKey, string? detail)
        => _channel.Enqueue(new LegacyUsageRecord(
            LegacyUsageCategory.MasterData,
            sourceKey.Trim(),
            null,
            null,
            detail));

    public LegacyStranglerDashboard GetDashboard(int top = 50, int recent = 100)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILegacyUsageTelemetryStore>();
        return store.GetDashboardAsync(top, recent, CancellationToken.None).GetAwaiter().GetResult();
    }
}
