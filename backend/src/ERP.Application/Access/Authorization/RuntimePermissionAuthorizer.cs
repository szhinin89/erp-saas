using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.Authorization;

public sealed class RuntimePermissionAuthorizer : IRuntimePermissionAuthorizer
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyContextProvider _companyContext;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;
    private readonly ICurrentCompany _currentCompany;

    public RuntimePermissionAuthorizer(
        ICurrentTenant currentTenant,
        ITenantRepository TenantRepository,
        ICompanyContextProvider companyContext,
        IEffectivePermissionKeysProvider permissionKeys,
        ICurrentCompany currentCompany
    )
    {
        _currentTenant = currentTenant;
        _tenantRepository = TenantRepository;
        _companyContext = companyContext;
        _permissionKeys = permissionKeys;
        _currentCompany = currentCompany;
    }

    public async Task<bool> IsAuthorizedAsync(
        string permissionKey,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default
    )
    {
        if (!(_currentTenant.TenantId != Guid.Empty))
            return false;

        var tenantId = _currentTenant.TenantId;

        if (await _tenantRepository.GetByIdAsync(tenantId, cancellationToken) is null)
            return false;

        if (userId == Guid.Empty)
            return false;

        // SEC-04: el bypass Admin sale del contexto operativo (rol de la membership en la empresa
        // operativa u operador global autorizado), nunca del rol del JWT — ese rol se emitió para
        // la empresa de apertura de sesión y no autoriza en otra empresa.
        var context = await _companyContext.ResolveOperationalForUserAsync(
            userId,
            cancellationToken
        );

        // SEC-04: un X-Company-Id presente sin membership activa (ni operador) es un rechazo de
        // contexto, no de permiso — mismo contrato que CompanyScopeBehavior (403
        // COMPANY_SCOPE_FORBIDDEN vía ExceptionMiddleware), sin caer en otra empresa del usuario.
        if (context is null && _currentCompany.HasCompanyContext)
            throw CompanyScopeException.AccessDenied();

        if (context is null)
            return false;

        if (context.HasAdminBypass)
            return true;

        if (
            context.CompanyId == Guid.Empty
            || !context.IsActiveMembership
            || context.ProfileId is null
        )
            return false;

        var allowed = await _permissionKeys.GetAllowedKeysAsync(
            tenantId,
            context.CompanyId,
            userId,
            context.ProfileId.Value,
            cancellationToken
        );

        return allowed.Any(k =>
            string.Equals(k, permissionKey, StringComparison.OrdinalIgnoreCase)
        );
    }
}
