using ERP.Application.Access.Caching;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public class RevokeCompanyUserMembershipHandler : IRequestHandler<RevokeCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public RevokeCompanyUserMembershipHandler(
        IAccessRepository accessRepository,
        ICurrentUser currentUser,
        ISubscriberRepository subscriberRepository,
        ICompanyProvisioningService companyProvisioning,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
        _subscriberRepository = subscriberRepository;
        _companyProvisioning = companyProvisioning;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(RevokeCompanyUserMembershipCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<object>> Handle(RevokeCompanyUserMembershipCommand command, CancellationToken ct)
    {
        var tenant = await _subscriberRepository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<object>.Success(new { });

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var email = command.UserEmail.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<object>.Success(new { });

        membership.Deactivate(_currentUser.UserId);
        await _accessRepository.SaveChangesAsync(ct);
        await _permissionsCache.InvalidateUserAsync(company.Id, user.Id, ct);
        return Result<object>.Success(new { });
    }
}
