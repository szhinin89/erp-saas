using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Dashboard;

/// <summary>
/// KPIs operativos tenant-scoped para el dashboard principal.
/// AsOf por defecto es "hoy" en el día operativo de la empresa (ICompanyClock) cuando es null —
/// DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01, nunca DateTime.UtcNow crudo. ZH-TEMPORAL-CONTRACT-02: AsOf es
/// fecha de negocio (DateOnly, API "YYYY-MM-DD"), nunca un instante.
/// </summary>
public sealed record GetDashboardKpisQuery(DateOnly? AsOf = null)
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
    DateOnly AsOf,
    int Month,
    int Year
);
