using System.Globalization;
using ERP.Application.Admin;
using ERP.Domain.Access.Entities;
using ERP.Domain.Subscriptions;
using ERP.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class GrowthAnalyticsReader : IGrowthAnalyticsReader
{
    private readonly ErpDbContext _db;

    public GrowthAnalyticsReader(ErpDbContext db) => _db = db;

    public async Task<GrowthAnalyticsResponseDto> GetSeriesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var g = (granularity ?? "month").Trim().ToLowerInvariant();
        if (g is not ("day" or "week" or "month"))
            g = "month";

        fromUtc = DateTime.SpecifyKind(fromUtc.Date, DateTimeKind.Utc);
        toUtc = DateTime.SpecifyKind(toUtc.Date, DateTimeKind.Utc);
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        var toExclusive = toUtc.AddDays(1);

        var tenantDates = await _db.Tenants.AsNoTracking()
            .Where(t => t.CreatedAt < toExclusive)
            .Select(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var userDates = await _db.IdentityUsers.AsNoTracking()
            .Where(u => u.CreatedAt < toExclusive)
            .Select(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        // IQF: métricas de crecimiento son agregados de plataforma (todas las empresas), no del tenant HTTP.
        var membershipDates = await _db.Memberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.CreatedAt < toExclusive)
            .Select(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var buckets = BuildBuckets(fromUtc, toUtc, g);
        var series = new List<GrowthAnalyticsBucketDto>(buckets.Count);

        foreach (var (start, end, label) in buckets)
        {
            var newT = CountInRange(tenantDates, start, end);
            var newU = CountInRange(userDates, start, end);
            var newM = CountInRange(membershipDates, start, end);

            var cumT = CountBefore(tenantDates, end);
            var cumU = CountBefore(userDates, end);
            var cumM = CountBefore(membershipDates, end);

            series.Add(new GrowthAnalyticsBucketDto(
                PeriodStart: start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PeriodEnd: end.AddTicks(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PeriodLabel: label,
                NewTenants: newT,
                NewIdentityUsers: newU,
                NewMemberships: newM,
                CumulativeTenants: cumT,
                CumulativeIdentityUsers: cumU,
                CumulativeMemberships: cumM));
        }

        return new GrowthAnalyticsResponseDto(
            From: fromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            To: toUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Granularity: g,
            Series: series);
    }

    public async Task<GrowthMonetaryResponseDto> GetMonetarySeriesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var g = (granularity ?? "month").Trim().ToLowerInvariant();
        if (g is not ("day" or "week" or "month"))
            g = "month";

        fromUtc = DateTime.SpecifyKind(fromUtc.Date, DateTimeKind.Utc);
        toUtc = DateTime.SpecifyKind(toUtc.Date, DateTimeKind.Utc);
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        var toExclusive = toUtc.AddDays(1);

        var plans = await _db.SaasPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new { p.Code, p.PriceAmount, p.Currency, p.BillingCycle })
            .ToListAsync(cancellationToken);

        var planMap = new Dictionary<string, (decimal Mrr, string Currency)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in plans)
        {
            var code = (p.Code ?? string.Empty).Trim().ToLowerInvariant();
            if (code.Length == 0)
                continue;
            var mrr = MonthlyEquivalentMrr(p.PriceAmount, p.BillingCycle);
            var cur = string.IsNullOrWhiteSpace(p.Currency) ? "USD" : p.Currency.Trim().ToUpperInvariant();
            planMap[code] = (mrr, cur);
        }

        var tenantRows = await _db.Tenants.AsNoTracking()
            .Where(t => t.CreatedAt < toExclusive)
            .Select(t => new { t.CreatedAt, t.IsActive, t.PlanCode })
            .ToListAsync(cancellationToken);

        var snapshots = new List<TenantMrrSnapshot>(tenantRows.Count);
        foreach (var tr in tenantRows)
        {
            var created = NormalizeCreatedUtc(tr.CreatedAt);
            if (!tr.IsActive)
            {
                snapshots.Add(new TenantMrrSnapshot(created, 0m, "USD"));
                continue;
            }

            var pc = (tr.PlanCode ?? string.Empty).Trim().ToLowerInvariant();
            if (pc.Length == 0 || !planMap.TryGetValue(pc, out var pm) || pm.Mrr <= 0m)
            {
                snapshots.Add(new TenantMrrSnapshot(created, 0m, "USD"));
                continue;
            }

            snapshots.Add(new TenantMrrSnapshot(created, pm.Mrr, pm.Currency));
        }

        var currencyHint = ResolveCurrencyHint(snapshots);
        var buckets = BuildBuckets(fromUtc, toUtc, g);
        var series = new List<GrowthMonetaryBucketDto>(buckets.Count);

        foreach (var (start, end, label) in buckets)
        {
            var newM = SumMrrInRange(snapshots, start, end);
            var cumM = SumMrrBefore(snapshots, end);
            series.Add(new GrowthMonetaryBucketDto(
                PeriodStart: start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PeriodEnd: end.AddTicks(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PeriodLabel: label,
                NewMrrApprox: newM,
                CumulativeMrrApprox: cumM));
        }

        return new GrowthMonetaryResponseDto(
            From: fromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            To: toUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Granularity: g,
            CurrencyHint: currencyHint,
            Series: series);
    }

    private readonly record struct TenantMrrSnapshot(DateTime CreatedUtc, decimal MrrApprox, string Currency);

    private static DateTime NormalizeCreatedUtc(DateTime d)
    {
        var x = d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime();
        return DateTime.SpecifyKind(x.Date, DateTimeKind.Utc);
    }

    private static decimal SumMrrInRange(List<TenantMrrSnapshot> items, DateTime start, DateTime endExclusive)
    {
        decimal s = 0;
        foreach (var x in items)
        {
            if (x.CreatedUtc >= start && x.CreatedUtc < endExclusive)
                s += x.MrrApprox;
        }

        return s;
    }

    private static decimal SumMrrBefore(List<TenantMrrSnapshot> items, DateTime endExclusive)
    {
        decimal s = 0;
        foreach (var x in items)
        {
            if (x.CreatedUtc < endExclusive)
                s += x.MrrApprox;
        }

        return s;
    }

    private static string ResolveCurrencyHint(IReadOnlyList<TenantMrrSnapshot> snapshots)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in snapshots)
        {
            if (x.MrrApprox <= 0m)
                continue;
            var c = string.IsNullOrWhiteSpace(x.Currency) ? "USD" : x.Currency.Trim().ToUpperInvariant();
            set.Add(c);
        }

        if (set.Count == 0)
            return "USD";
        if (set.Count == 1)
            return set.First();

        return "MIXED";
    }

    private static decimal MonthlyEquivalentMrr(decimal priceAmount, string billingCycle)
    {
        var c = (billingCycle ?? string.Empty).Trim().ToLowerInvariant();
        if (c == SaasBillingCycle.Yearly)
            return priceAmount / 12m;
        if (c == SaasBillingCycle.Quarterly)
            return priceAmount / 3m;
        if (c == SaasBillingCycle.OneTime)
            return 0m;

        return priceAmount;
    }

    private static int CountInRange(IReadOnlyList<DateTime> dates, DateTime start, DateTime endExclusive)
    {
        var n = 0;
        foreach (var d in dates)
        {
            var x = d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime();
            if (x >= start && x < endExclusive)
                n++;
        }

        return n;
    }

    private static int CountBefore(IReadOnlyList<DateTime> dates, DateTime endExclusive)
    {
        var n = 0;
        foreach (var d in dates)
        {
            var x = d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime();
            if (x < endExclusive)
                n++;
        }

        return n;
    }

    private static List<(DateTime Start, DateTime EndExclusive, string Label)> BuildBuckets(
        DateTime fromUtc,
        DateTime toUtc,
        string g)
    {
        var list = new List<(DateTime, DateTime, string)>();
        var toExclusive = toUtc.AddDays(1);

        if (g == "day")
        {
            for (var d = fromUtc; d < toExclusive; d = d.AddDays(1))
            {
                var end = d.AddDays(1);
                list.Add((d, end, d.ToString("d MMM", CultureInfo.InvariantCulture)));
            }

            return list;
        }

        if (g == "week")
        {
            var cursor = fromUtc;
            while (cursor < toExclusive)
            {
                var end = cursor.AddDays(7);
                if (end > toExclusive)
                    end = toExclusive;
                var label = $"{cursor:dd MMM} – {end.AddDays(-1):dd MMM yyyy}";
                list.Add((cursor, end, label));
                cursor = end;
            }

            return list;
        }

        // month
        var m = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (m < toExclusive)
        {
            var end = m.AddMonths(1);
            if (end > toExclusive)
                end = toExclusive;
            var label = m.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            list.Add((m, end, label));
            m = m.AddMonths(1);
        }

        return list;
    }
}
