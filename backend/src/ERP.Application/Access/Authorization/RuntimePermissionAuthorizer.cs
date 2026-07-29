using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.Authorization;

public sealed class RuntimePermissionAuthorizer : IRuntimePermissionAuthorizer
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyContextProvider _companyContext;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;

    public RuntimePermissionAuthorizer(
        ICurrentTenant currentTenant,
        ITenantRepository TenantRepository,
        ICompanyContextProvider companyContext,
        IEffectivePermissionKeysProvider permissionKeys)
    {
        _currentTenant = currentTenant;
        _tenantRepository = TenantRepository;
        _companyContext = companyContext;
        _permissionKeys = permissionKeys;
    }

    public async Task<bool> IsAuthorizedAsync(
        string permissionKey, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!(_currentTenant.TenantId != Guid.Empty))
            return false;

        var tenantId = _currentTenant.TenantId;

        if (await _tenantRepository.GetByIdAsync(tenantId, cancellationToken) is null)
            return false;

        if (string.Equals(role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        if (userId == Guid.Empty)
            return false;

        var context = await _companyContext.ResolveOperationalForUserAsync(userId, cancellationToken);
        if (context is null
            || context.CompanyId == Guid.Empty
            || !context.IsActiveMembership
            || context.ProfileId is null)
            return false;

        var allowed = await _permissionKeys.GetAllowedKeysAsync(
            tenantId, context.CompanyId, userId, context.ProfileId.Value, cancellationToken);

        return allowed.Any(k => string.Equals(k, permissionKey, StringComparison.OrdinalIgnoreCase));
    }
}
