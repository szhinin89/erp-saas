using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Parámetro general de configuración de la empresa (clave-valor).
///
/// MULTI-TENANT: ITenantScopedEntity para que EnterpriseQueryFilterConfigurator aplique
/// el filtro automático tenant_id = current_tenant. Resuelve el warning de EF Core
/// sobre Company (ITenantScopedEntity) siendo el required end de esta relación.
/// </summary>
public class GeneralParameter : ITenantScopedEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant owner — required for multi-tenant query filter.</summary>
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Key { get; set; } = null!;
    public string? Value { get; set; }
    public string? Description { get; set; }

    public Company Company { get; set; } = null!;
}
