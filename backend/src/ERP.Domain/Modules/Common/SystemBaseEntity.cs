namespace ERP.Domain.Common;

/// <summary>
/// Entidad a nivel sistema (no multi-tenant).
/// Se usa para identidad/global settings que no deben pasar por el filtro TenantId.
/// </summary>
public abstract class SystemBaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

