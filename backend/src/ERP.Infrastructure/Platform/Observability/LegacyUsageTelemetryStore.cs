using ERP.Application.Platform.Observability;
using ERP.Domain.Platform.Observability.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Platform.Observability;

public sealed record LegacyUsageRecord(
    LegacyUsageCategory Category,
    string UsageKey,
    string? Successor,
    string? CallerIp,
    string? Detail);

public interface ILegacyUsageTelemetryChannel
{
    void Enqueue(LegacyUsageRecord record);
}

public sealed class LegacyUsageTelemetryChannel : ILegacyUsageTelemetryChannel
{
    private readonly System.Threading.Channels.Channel<LegacyUsageRecord> _channel =
        System.Threading.Channels.Channel.CreateBounded<LegacyUsageRecord>(
            new System.Threading.Channels.BoundedChannelOptions(10_000)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

    public System.Threading.Channels.ChannelReader<LegacyUsageRecord> Reader => _channel.Reader;

    public void Enqueue(LegacyUsageRecord record) => _channel.Writer.TryWrite(record);
}

public sealed class LegacyUsageTelemetryStore : ILegacyUsageTelemetryStore
{
    private readonly ErpDbContext _db;

    public LegacyUsageTelemetryStore(ErpDbContext db) => _db = db;

    public Task RecordAsync(
        LegacyUsageCategory category,
        string usageKey,
        string? successor,
        string? callerIp,
        string? detail,
        CancellationToken ct = default)
        => RecordInternalAsync(new LegacyUsageRecord(category, usageKey, successor, callerIp, detail), ct);

    public async Task RecordInternalAsync(LegacyUsageRecord record, CancellationToken ct)
    {
        var stat = await _db.LegacyUsageStats
            .FirstOrDefaultAsync(
                x => x.Category == record.Category && x.UsageKey == record.UsageKey,
                ct);

        if (stat is null)
        {
            stat = LegacyUsageStat.Create(
                record.Category,
                record.UsageKey,
                record.Successor,
                record.CallerIp,
                record.Detail);
            _db.LegacyUsageStats.Add(stat);
        }
        else
        {
            stat.RecordHit(record.CallerIp, record.Detail, record.Successor);
        }

        _db.LegacyUsageHits.Add(LegacyUsageHit.Create(
            record.Category,
            record.UsageKey,
            record.Successor,
            record.CallerIp,
            record.Detail));

        await _db.SaveChangesAsync(ct);

        // Trim old hits (keep last 5000 rows per category batch — best effort)
        var overflow = await _db.LegacyUsageHits
            .OrderByDescending(x => x.HitAtUtc)
            .Skip(5000)
            .Take(500)
            .ToListAsync(ct);
        if (overflow.Count > 0)
        {
            _db.LegacyUsageHits.RemoveRange(overflow);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<LegacyStranglerDashboard> GetDashboardAsync(int top, int recent, CancellationToken ct)
    {
        top = Math.Clamp(top, 1, 200);
        recent = Math.Clamp(recent, 1, 500);

        var allStats = await _db.LegacyUsageStats.AsNoTracking().ToListAsync(ct);
        var endpointStats = allStats.Where(x => x.Category == LegacyUsageCategory.Endpoint).ToList();
        var uiStats = allStats.Where(x => x.Category == LegacyUsageCategory.UiRoute).ToList();
        var mdStats = allStats.Where(x => x.Category == LegacyUsageCategory.MasterData).ToList();

        var totalEndpointHits = endpointStats.Sum(x => x.TotalHits);
        var totalUiHits = uiStats.Sum(x => x.TotalHits);
        var totalMdHits = mdStats.Sum(x => x.TotalHits);
        var totalLegacyHits = totalEndpointHits + totalUiHits + totalMdHits;

        static IReadOnlyList<LegacyEndpointUsageSummary> MapTop(
            IEnumerable<LegacyUsageStat> stats,
            int take)
        {
            return stats
                .OrderByDescending(x => x.TotalHits)
                .Take(take)
                .Select(x =>
                {
                    var (method, endpoint) = SplitEndpointKey(x.UsageKey);
                    return new LegacyEndpointUsageSummary(
                        endpoint,
                        method,
                        x.TotalHits,
                        x.LastHitUtc,
                        x.Successor,
                        x.LastCallerIp);
                })
                .ToList();
        }

        static IReadOnlyList<LegacyUiUsageSummary> MapUi(IEnumerable<LegacyUsageStat> stats, int take)
            => stats.OrderByDescending(x => x.TotalHits).Take(take)
                .Select(x => new LegacyUiUsageSummary(x.UsageKey, x.TotalHits, x.LastHitUtc, x.LastDetail))
                .ToList();

        static IReadOnlyList<LegacyMasterDataUsageSummary> MapMd(IEnumerable<LegacyUsageStat> stats, int take)
            => stats.OrderByDescending(x => x.TotalHits).Take(take)
                .Select(x => new LegacyMasterDataUsageSummary(x.UsageKey, x.TotalHits, x.LastHitUtc, x.LastDetail))
                .ToList();

        var recentHits = await _db.LegacyUsageHits.AsNoTracking()
            .OrderByDescending(x => x.HitAtUtc)
            .Take(recent)
            .Select(x => new LegacyEndpointRecentHit(
                SplitEndpointKey(x.UsageKey).Endpoint,
                SplitEndpointKey(x.UsageKey).Method,
                x.Successor,
                x.HitAtUtc,
                x.CallerIp))
            .ToListAsync(ct);

        // Migration % heuristic: endpoints with zero hits in last period vs known deprecated surface
        const int knownDeprecatedSurface = 25;
        var activeLegacyEndpoints = endpointStats.Count(x => x.TotalHits > 0);
        var migrationPercent = knownDeprecatedSurface <= 0
            ? 100m
            : Math.Round(Math.Clamp(100m - (activeLegacyEndpoints * 100m / knownDeprecatedSurface), 0m, 100m), 1);

        return new LegacyStranglerDashboard(
            TopEndpoints: MapTop(endpointStats, top),
            TopUiRoutes: MapUi(uiStats, top),
            TopMasterDataSources: MapMd(mdStats, top),
            RecentHits: recentHits,
            TotalEndpointHits: totalEndpointHits,
            TotalUiHits: totalUiHits,
            TotalMasterDataHits: totalMdHits,
            TotalHits: totalLegacyHits,
            MigrationCompletePercent: migrationPercent);
    }

    private static (string Method, string Endpoint) SplitEndpointKey(string key)
    {
        var space = key.IndexOf(' ');
        if (space <= 0)
            return ("GET", key);
        return (key[..space], key[(space + 1)..]);
    }
}

public sealed class LegacyUsageTelemetryWorker : BackgroundService
{
    private readonly LegacyUsageTelemetryChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LegacyUsageTelemetryWorker> _logger;

    public LegacyUsageTelemetryWorker(
        LegacyUsageTelemetryChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<LegacyUsageTelemetryWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var record in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<ILegacyUsageTelemetryStore>();
                await store.RecordAsync(
                    record.Category,
                    record.UsageKey,
                    record.Successor,
                    record.CallerIp,
                    record.Detail,
                    stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to persist legacy usage telemetry for {Key}", record.UsageKey);
            }
        }
    }
}
