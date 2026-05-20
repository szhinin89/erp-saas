using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Subscriptions;

/// <summary>
/// Resuelve módulos habilitados para JWT, permisos de sesión y DTOs de tenant.
/// Respeta <see cref="Common.Config.SaasEntitlementsOptions.PreferLegacyEnabledModulesJsonForSession"/>.
/// </summary>
public interface ISessionModulesResolver
{
    Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(
        Guid tenantId,
        Tenant? legacyTenantRow = null,
        CancellationToken ct = default);
}
