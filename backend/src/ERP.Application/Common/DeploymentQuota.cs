using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Common;

/// <summary>
/// Cuotas de instancia on-prem / dedicada (<see cref="IDeploymentFeatureFlags"/>).
/// Ilimitado: omitir la clave, valor vacío, <c>unlimited</c>, o un entero ≤ 0.
/// </summary>
public static class DeploymentQuota
{
    public static async Task<string?> GetBlockingReasonIfAtActiveTenantCapAsync(
        IDeploymentFeatureFlags flags,
        ITenantRepository tenants,
        CancellationToken ct)
    {
        if (flags.IsDedicatedSingleClientInstance && flags.MaxActiveTenants is null)
        {
            return "Instancia dedicada (un cliente / servidor propio): defina un tope de empresas (RUC). " +
                   "No se permite ilimitado. Use Deployment:MaxActiveTenants o el archivo App_Data/instance-quota.json (maxActiveTenants).";
        }

        var max = flags.MaxActiveTenants;
        if (max is null)
            return null;

        var all = await tenants.GetAllAsync(ct);
        var active = all.Count(t => t.IsActive);
        if (active >= max.Value)
        {
            return $"Se alcanzó el máximo de empresas activas para esta instancia ({max.Value}). " +
                   "Aumente el tope, desactive una empresa o contacte al operador de plataforma.";
        }

        return null;
    }

    /// <summary>Tope de usuarios Identity con membresía activa por empresa (tenant).</summary>
    public static async Task<string?> GetBlockingReasonIfAtTenantMembershipUserCapAsync(
        IDeploymentFeatureFlags flags,
        IAccessRepository access,
        Guid tenantId,
        CancellationToken ct)
    {
        var max = flags.MaxUsersPerTenant;
        if (max is null)
            return null;

        var n = await access.CountActiveMembershipsByTenantAsync(tenantId, ct);
        if (n >= max.Value)
        {
            return $"Se alcanzó el máximo de usuarios por empresa ({max.Value}). " +
                   "Aumente el tope (Deployment:MaxUsersPerTenant o App_Data/instance-quota.json) o desactive usuarios.";
        }

        return null;
    }

    /// <summary>Antes de crear un <c>IdentityUser</c> nuevo (no aplica al vincular admin existente).</summary>
    public static async Task<string?> GetBlockingReasonIfAtIdentityUserCapAsync(
        IDeploymentFeatureFlags flags,
        IAccessRepository access,
        CancellationToken ct)
    {
        var max = flags.MaxIdentityUsers;
        if (max is null)
            return null;

        var n = await access.CountIdentityUsersAsync(ct);
        if (n >= max.Value)
        {
            return $"Se alcanzó el máximo de usuarios globales (Identity) para esta instancia ({max.Value}). " +
                   "Aumente Deployment:MaxIdentityUsers o contacte al operador de plataforma.";
        }

        return null;
    }
}
