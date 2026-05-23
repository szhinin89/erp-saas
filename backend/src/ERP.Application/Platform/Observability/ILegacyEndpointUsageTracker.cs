namespace ERP.Application.Platform.Observability;

public sealed record LegacyEndpointUsageSummary(
    string Endpoint,
    string Method,
    long TotalHits,
    DateTimeOffset? LastHitUtc,
    string? Successor,
    string? LastCallerIp);

public sealed record LegacyUiUsageSummary(
    string RouteKey,
    long TotalHits,
    DateTimeOffset? LastHitUtc,
    string? LastReferrer);

public sealed record LegacyMasterDataUsageSummary(
    string SourceKey,
    long TotalHits,
    DateTimeOffset? LastHitUtc,
    string? LastDetail);

public sealed record LegacyEndpointRecentHit(
    string Endpoint,
    string Method,
    string? Successor,
    DateTimeOffset AtUtc,
    string? CallerIp);

public sealed record LegacyStranglerDashboard(
    IReadOnlyList<LegacyEndpointUsageSummary> TopEndpoints,
    IReadOnlyList<LegacyUiUsageSummary> TopUiRoutes,
    IReadOnlyList<LegacyMasterDataUsageSummary> TopMasterDataSources,
    IReadOnlyList<LegacyEndpointRecentHit> RecentHits,
    long TotalEndpointHits,
    long TotalUiHits,
    long TotalMasterDataHits,
    long TotalHits,
    decimal MigrationCompletePercent);

public interface ILegacyEndpointUsageTracker
{
    void Record(string endpoint, string method, string? successor, string? callerIp);

    void RecordUiRoute(string routeKey, string? referrer);

    void RecordMasterData(string sourceKey, string? detail);

    LegacyStranglerDashboard GetDashboard(int top = 50, int recent = 100);
}
