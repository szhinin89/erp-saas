namespace ERP.Application.Common;

/// <summary>Motivo documentado para consultar sin el filtro global de tenant.</summary>
public enum PlatformQueryReason
{
    TenantScopedExplicit,
    CrossTenantSystem,
    PlatformMetrics,
    BackgroundJob,
    Seeding,
    DbContextSync,
    DevOnly,
}
