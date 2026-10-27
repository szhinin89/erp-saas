using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public class RevokeCompanyUserMembershipHandler
    : IRequestHandler<RevokeCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder _navigationBuilder;
    private readonly ICompanyContextProvider _companyContext;

    public RevokeCompanyUserMembershipHandler(
        IAccessRepository accessRepository,
        ICurrentUser currentUser,
        ITenantRepository TenantRepository,
        ICompanyRepository companyRepository,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder,
        ICompanyContextProvider companyContext
    )
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
        _tenantRepository = TenantRepository;
        _companyRepository = companyRepository;
        _permissionsCache = permissionsCache;
        _navigationBuilder = navigationBuilder;
        _companyContext = companyContext;
    }

    public Task<Result<object>> HandleAsync(
        RevokeCompanyUserMembershipCommand command,
        CancellationToken cancellationToken = default
    ) => Handle(command, cancellationToken);

    public async Task<Result<object>> Handle(
        RevokeCompanyUserMembershipCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken);
        if (tenant is null)
            return Result<object>.Success(new { });

        var company = await _companyRepository.GetByIdForTenantAsync(
            command.CompanyId,
            command.TenantId,
            cancellationToken
        );
        if (company is null)
            return Result<object>.NotFound("Empresa no encontrada para el tenant.");

        var username = command.Username.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByUsernameAsync(username, cancellationToken);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            company.Id,
            user.Id,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return Result<object>.Success(new { });

        // SEC-05: retirar una membership Admin es retirar el rol Admin — solo un Admin de esta
        // empresa u operador global (mismo bypass que RuntimePermissionAuthorizer).
        if (string.Equals(membership.Role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var actor = await _companyContext.ResolveOperationalForCurrentUserAsync(
                cancellationToken
            );
            if (actor is null || actor.CompanyId != company.Id || !actor.HasAdminBypass)
                return Result<object>.Forbidden(
                    "Solo un administrador de la empresa puede retirar el rol Admin."
                );
        }

        // CompanyAdministratorInvariant: la empresa debe conservar al menos un Administrador
        // activo tras la revocación — regla pura, el handler obtiene las memberships activas y
        // se las entrega, nunca al revés.
        var activeMemberships = await _accessRepository.GetCompanyUserMembershipsByCompanyAsync(
            company.Id,
            onlyActive: true,
            cancellationToken
        );
        var invariantViolation = CompanyAdministratorInvariant.Validate<object>(
            membership,
            new CompanyAdministratorInvariant.FutureState(IsActive: false, Role: membership.Role),
            activeMemberships
        );
        if (invariantViolation is not null)
            return invariantViolation;

        // SEC-06: la revocación se persiste dentro del protocolo de invalidación fail-closed del
        // usuario en la empresa (la autorización además la rechaza por membership inactiva).
        return await _permissionsCache.RunAsync(
            PermissionsCacheScope.User(company.Id, user.Id),
            async ct =>
            {
                membership.Deactivate(_currentUser.UserId);
                await _accessRepository.SaveChangesAsync(ct);
                _navigationBuilder.InvalidateCache(command.TenantId, company.Id, user.Id);
                return Result<object>.Success(new { });
            },
            cancellationToken
        );
    }
}
