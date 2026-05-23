using Microsoft.EntityFrameworkCore;
using ERP.Application.Modules.Dashboard;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Reads operational KPIs for the dashboard using direct EF Core queries.
/// Uses global query filters (subscriber scoped) — no manual subscriber_id needed in most queries.
/// </summary>
public sealed class DashboardKpiReader : IDashboardKpiReader
{
    private readonly ErpDbContext _db;

    public DashboardKpiReader(ErpDbContext db) => _db = db;

    public async Task<DashboardKpisDto> ReadAsync(
        Guid     subscriberId,
        Guid     companyId,
        DateTime asOf,
        CancellationToken ct = default)
    {
        var monthStart = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearStart  = new DateTime(asOf.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd     = asOf.AddDays(1);

        // ── Sales MTD / YTD ───────────────────────────────────────────────
        // Global filter already applies subscriber_id; filter company explicitly
        // SalesBill.Status is a string: "Borrador", "Validado", "Autorizado", "Anulado", etc.
        var salesMtd = await _db.SalesBills
            .Where(b => b.CompanyId == companyId
                     && b.Status != "Anulado"
                     && b.IssueDate >= monthStart
                     && b.IssueDate < dayEnd)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(b => b.Total), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var salesYtd = await _db.SalesBills
            .Where(b => b.CompanyId == companyId
                     && b.Status != "Anulado"
                     && b.IssueDate >= yearStart
                     && b.IssueDate < dayEnd)
            .SumAsync(b => b.Total, ct);

        // ── AR ────────────────────────────────────────────────────────────
        // Global filter applies subscriber; filter company explicitly
        var arEntries = await _db.ArEntries
            .Where(e => e.CompanyId == companyId
                     && e.Status != AccountsReceivableEntry.StatusPaid
                     && e.Status != AccountsReceivableEntry.StatusCancelled)
            .Select(e => new { e.RemainingAmount, e.DueDate, e.Status })
            .ToListAsync(ct);

        var pendingArTotal = arEntries.Sum(e => e.RemainingAmount);
        var overdueAr      = arEntries.Where(e => e.DueDate < asOf).ToList();

        // ── AP ────────────────────────────────────────────────────────────
        var apEntries = await _db.ApEntries
            .Where(e => e.CompanyId == companyId
                     && e.Status != AccountsPayableEntry.StatusPaid
                     && e.Status != AccountsPayableEntry.StatusCancelled)
            .Select(e => new { e.RemainingAmount, e.DueDate })
            .ToListAsync(ct);

        var pendingApTotal = apEntries.Sum(e => e.RemainingAmount);
        var overdueAp      = apEntries.Where(e => e.DueDate < asOf).ToList();

        // ── Inventory ─────────────────────────────────────────────────────
        // CurrentStock is ICompanyOperationalEntity (CompanyId nullable)
        var stockCounts = await _db.CurrentStocks
            .Where(s => s.CompanyId == companyId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                LowStock  = g.Count(s => s.Quantity > 0 && s.Quantity <= 5),
                OutOfStock = g.Count(s => s.Quantity <= 0),
            })
            .FirstOrDefaultAsync(ct);

        return new DashboardKpisDto(
            SalesMtd:         salesMtd?.Total     ?? 0m,
            InvoicesMtd:      salesMtd?.Count     ?? 0,
            SalesYtd:         salesYtd,
            PendingArTotal:   pendingArTotal,
            PendingArCount:   arEntries.Count,
            OverdueArTotal:   overdueAr.Sum(e => e.RemainingAmount),
            OverdueArCount:   overdueAr.Count,
            PendingApTotal:   pendingApTotal,
            PendingApCount:   apEntries.Count,
            OverdueApTotal:   overdueAp.Sum(e => e.RemainingAmount),
            OverdueApCount:   overdueAp.Count,
            LowStockSkuCount: stockCounts?.LowStock   ?? 0,
            OutOfStockSkuCount: stockCounts?.OutOfStock ?? 0,
            AsOf:  asOf,
            Month: asOf.Month,
            Year:  asOf.Year);
    }
}
