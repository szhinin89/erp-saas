using ERP.Application.Access.Caching;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembership;

public class UpsertCompanyUserMembershipHandler : IRequestHandler<UpsertCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public UpsertCompanyUserMembershipHandler(
        IAccessRepository accessRepository,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        ISubscriberRepository subscriberRepository,
        ICompanyProvisioningService companyProvisioning,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
        _deployment = deployment;
        _subscriberRepository = subscriberRepository;
        _companyProvisioning = companyProvisioning;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(UpsertCompanyUserMembershipCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<object>> Handle(UpsertCompanyUserMembershipCommand command, CancellationToken ct)
    {
        if (string.Equals(command.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return Result<object>.Failure(
                "Solo puede existir un SuperAdmin por servidor (tabla users). No se asigna por membresía IAM.");
        }

        var tenant = await _subscriberRepository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<object>.Failure("Subscriber no encontrado.");

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var email = command.UserEmail.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var existing = await _accessRepository.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
        if (existing is null)
        {
            var cap = await DeploymentQuota.GetBlockingReasonIfAtTenantCompanyUserMembershipUserCapAsync(
                _deployment, _accessRepository, command.SubscriberId, ct);
            if (cap is not null)
                return Result<object>.Failure(cap);

            var membership = CompanyUserMembership.Create(company.Id, user.Id, command.Role, command.ProfileId, createdBy: _currentUser.UserId);
            await _accessRepository.AddCompanyUserMembershipAsync(membership, ct);
        }
        else
        {
            if (!existing.IsActive)
            {
                var capRe = await DeploymentQuota.GetBlockingReasonIfAtTenantCompanyUserMembershipUserCapAsync(
                    _deployment, _accessRepository, command.SubscriberId, ct);
                if (capRe is not null)
                    return Result<object>.Failure(capRe);
            }

            existing.Activate(command.Role, command.ProfileId, updatedBy: _currentUser.UserId);
        }

        await _accessRepository.SaveChangesAsync(ct);
        await _permissionsCache.InvalidateUserAsync(company.Id, user.Id, ct);
        return Result<object>.Success(new { });
    }
}
