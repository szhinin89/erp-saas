using ERP.Application.Modules.Dashboard;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Platform Kernel stub — returns zero KPIs until transactional modules are rebuilt.
/// </summary>
public sealed class DashboardKpiReader : IDashboardKpiReader
{
    public Task<DashboardKpisDto> ReadAsync(
        Guid tenantId,
        Guid companyId,
        DateTime asOf,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new DashboardKpisDto(
            SalesMtd:           0m,
            InvoicesMtd:        0,
            SalesYtd:           0m,
            PendingArTotal:     0m,
            PendingArCount:     0,
            OverdueArTotal:     0m,
            OverdueArCount:     0,
            PendingApTotal:     0m,
            PendingApCount:     0,
            OverdueApTotal:     0m,
            OverdueApCount:     0,
            LowStockSkuCount:   0,
            OutOfStockSkuCount: 0,
            AsOf:  asOf,
            Month: asOf.Month,
            Year:  asOf.Year));
}
