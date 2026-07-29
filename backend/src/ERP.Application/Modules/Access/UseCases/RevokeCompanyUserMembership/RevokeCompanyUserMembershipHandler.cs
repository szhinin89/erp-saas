using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public class RevokeCompanyUserMembershipHandler
    : IRequestHandler<RevokeCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder _navigationBuilder;

    public RevokeCompanyUserMembershipHandler(
        IAccessRepository accessRepository,
        ICurrentUser currentUser,
        ITenantRepository TenantRepository,
        ICompanyProvisioningService companyProvisioning,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder
    )
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
        _tenantRepository = TenantRepository;
        _companyProvisioning = companyProvisioning;
        _permissionsCache = permissionsCache;
        _navigationBuilder = navigationBuilder;
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

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(
            tenant,
            cancellationToken
        );

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

        membership.Deactivate(_currentUser.UserId);
        await _accessRepository.SaveChangesAsync(cancellationToken);
        await _permissionsCache.InvalidateUserAsync(company.Id, user.Id, cancellationToken);
        _navigationBuilder.InvalidateCache(command.TenantId, company.Id, user.Id);
        return Result<object>.Success(new { });
    }
}
