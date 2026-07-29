namespace ERP.Application.Modules.Dashboard;

/// <summary>
/// Lector de KPIs operativos para el dashboard. Implementación en Infrastructure
/// para acceso directo a DB con queries optimizadas.
/// </summary>
public interface IDashboardKpiReader
{
    Task<DashboardKpisDto> ReadAsync(
        Guid tenantId,
        Guid companyId,
        DateTime asOf,
        CancellationToken cancellationToken = default);
}
