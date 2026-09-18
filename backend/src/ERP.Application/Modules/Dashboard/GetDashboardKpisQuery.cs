using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Dashboard;

/// <summary>
/// KPIs operativos tenant-scoped para el dashboard principal.
/// AsOf por defecto es "hoy" en el día operativo de la empresa (ICompanyClock) cuando es null —
/// DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01, nunca DateTime.UtcNow crudo.
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
