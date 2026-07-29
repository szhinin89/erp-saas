using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Dashboard;

/// <summary>
/// KPIs operativos tenant-scoped para el dashboard principal.
/// AsOf defaults to today (UTC) when null.
/// </summary>
public sealed record GetDashboardKpisQuery(DateTime? AsOf = null)
    : IRequest<Result<DashboardKpisDto>>,
        ICompanyScopedRequest;

public sealed record DashboardKpisDto(
    // ── Sales ─────────────────────────────────────────────────────────────
    decimal SalesMtd,
    int InvoicesMtd,
    decimal SalesYtd,
    // ── AR ────────────────────────────────────────────────────────────────
    decimal PendingArTotal,
    int PendingArCount,
    decimal OverdueArTotal,
    int OverdueArCount,
    // ── AP ────────────────────────────────────────────────────────────────
    decimal PendingApTotal,
    int PendingApCount,
    decimal OverdueApTotal,
    int OverdueApCount,
    // ── Inventory ─────────────────────────────────────────────────────────
    int LowStockSkuCount,
    int OutOfStockSkuCount,
    // ── Meta ──────────────────────────────────────────────────────────────
    DateTime AsOf,
    int Month,
    int Year
);
