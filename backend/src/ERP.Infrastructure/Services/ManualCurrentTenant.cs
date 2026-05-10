using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación de ICurrentTenant con TenantId fijo, usada en background services
/// donde no hay HttpContext ni JWT disponible.
/// </summary>
public sealed class ManualCurrentTenant : ICurrentTenant
{
    public Guid TenantId      { get; }
    public bool IsAuthenticated => true;

    public ManualCurrentTenant(Guid tenantId) => TenantId = tenantId;
}
