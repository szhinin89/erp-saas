using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Interfaces;

/// <summary>
/// Acceso de bajo nivel a org_settings. Solo los use cases de configuración
/// deben consumir este repositorio directamente.
/// Cualquier otro módulo debe usar <see cref="IOrgConfigResolver"/>.
/// </summary>
public interface IOrgSettingsRepository
{
    Task<OrgSetting?> GetAsync(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        string key,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<OrgSetting>> GetAllForScopeAsync(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        CancellationToken ct = default
    );

    Task UpsertAsync(OrgSetting setting, CancellationToken ct = default);

    Task DeleteAsync(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        string key,
        CancellationToken ct = default
    );

    Task SaveChangesAsync(CancellationToken ct = default);
}
