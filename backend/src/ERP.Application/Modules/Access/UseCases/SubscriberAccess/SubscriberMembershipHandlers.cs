using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.SubscriberAccess;

public class GetSubscriberCompanyUserMembershipsHandler : IRequestHandler<GetSubscriberCompanyUserMembershipsQuery, Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _tenant;

    public GetSubscriberCompanyUserMembershipsHandler(IAccessRepository repo, ICurrentSubscriber tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public Task<Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetSubscriberCompanyUserMembershipsQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>> Handle(GetSubscriberCompanyUserMembershipsQuery request, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var company_user_memberships = await _repo.GetCompanyUserMembershipsBySubscriberAsync(subscriberId, request.OnlyActive, ct);

        // En este MVP, solo retornamos company_user_memberships. Los detalles del usuario se leen por Id (sin joins complejos).
        // Para UX, hacemos lookup por email/nombre desde IdentityUsers.
        var users = new Dictionary<Guid, IdentityUser>();
        foreach (var m in company_user_memberships)
        {
            if (users.ContainsKey(m.IdentityUserId)) continue;
            var u = await _repo.GetUserByIdAsync(m.IdentityUserId, ct);
            if (u is not null) users[m.IdentityUserId] = u;
        }

        var items = company_user_memberships.Select(m =>
        {
            users.TryGetValue(m.IdentityUserId, out var u);
            return new SubscriberCompanyUserMembershipItemDto(
                IdentityUserId: m.IdentityUserId,
                Email: u?.Email.Value ?? "",
                FullName: u?.FullName ?? "",
                Role: m.Role,
                ProfileId: m.ProfileId,
                IsActive: m.IsActive
            );
        }).ToList();

        return Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>.Success(items);
    }
}

public class SubscriberUpsertCompanyUserMembershipHandler : IRequestHandler<SubscriberUpsertCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public SubscriberUpsertCompanyUserMembershipHandler(
        IAccessRepository repo,
        ICurrentSubscriber tenant,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        ICompanyProvisioningService companyProvisioning,
        ISubscriberRepository subscriberRepository,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _repo = repo;
        _tenant = tenant;
        _currentUser = currentUser;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _companyProvisioning = companyProvisioning;
        _subscriberRepository = subscriberRepository;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(SubscriberUpsertCompanyUserMembershipCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<object>> Handle(SubscriberUpsertCompanyUserMembershipCommand cmd, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<object>.Failure("Subscriber inválido.");

        if (string.Equals(cmd.Role, PlatformAuthConstants.JwtPlatformOperatorRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<object>.Failure(
                "Solo puede existir un operador platform primario por servidor. No se asigna por membresía IAM.");
        }

        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetUserByEmailAsync(email, ct);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(cmd.FirstName) ||
                string.IsNullOrWhiteSpace(cmd.LastName) ||
                string.IsNullOrWhiteSpace(cmd.Password))
            {
                return Result<object>.Failure("Para crear un usuario nuevo se requiere nombre, apellido y contraseña.");
            }

            var userCap = await DeploymentQuota.GetBlockingReasonIfAtIdentityUserCapAsync(_deployment, _repo, ct);
            if (userCap is not null)
                return Result<object>.Failure(userCap);

            var hash = _passwordHasher.HashPassword(cmd.Password);
            user = IdentityUser.Create(cmd.FirstName!, cmd.LastName!, email, hash, _currentUser.UserId);
            await _repo.AddUserAsync(user, ct);
        }

        var tenant = await _subscriberRepository.GetByIdAsync(subscriberId, ct);
        if (tenant is null)
            return Result<object>.Failure("Subscriber inválido.");

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);

        var membership = await _repo.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
        if (membership is null)
        {
            var cap = await DeploymentQuota.GetBlockingReasonIfAtSubscriberCompanyUserMembershipUserCapAsync(_deployment, _repo, subscriberId, ct);
            if (cap is not null)
                return Result<object>.Failure(cap);

            membership = CompanyUserMembership.Create(company.Id, user.Id, cmd.Role, cmd.ProfileId, _currentUser.UserId);
            await _repo.AddCompanyUserMembershipAsync(membership, ct);
        }
        else
        {
            if (!membership.IsActive)
            {
                var capRe = await DeploymentQuota.GetBlockingReasonIfAtSubscriberCompanyUserMembershipUserCapAsync(_deployment, _repo, subscriberId, ct);
                if (capRe is not null)
                    return Result<object>.Failure(capRe);
            }

            membership.Activate(cmd.Role, cmd.ProfileId, _currentUser.UserId);
        }

        await _repo.SaveChangesAsync(ct);
        await _permissionsCache.InvalidateUserAsync(company.Id, user.Id, ct);
        return Result<object>.Success(new { });
    }
}

public class SubscriberRevokeCompanyUserMembershipHandler : IRequestHandler<SubscriberRevokeCompanyUserMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public SubscriberRevokeCompanyUserMembershipHandler(
        IAccessRepository repo,
        ICurrentSubscriber tenant,
        ICurrentUser currentUser,
        ICompanyProvisioningService companyProvisioning,
        ISubscriberRepository subscriberRepository,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _repo = repo;
        _tenant = tenant;
        _currentUser = currentUser;
        _companyProvisioning = companyProvisioning;
        _subscriberRepository = subscriberRepository;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(SubscriberRevokeCompanyUserMembershipCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<object>> Handle(SubscriberRevokeCompanyUserMembershipCommand cmd, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Success(new { });

        var tenant = await _subscriberRepository.GetByIdAsync(subscriberId, ct);
        if (tenant is null)
            return Result<object>.Success(new { });

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        var membership = await _repo.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<object>.Success(new { });

        membership.Deactivate(_currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        await _permissionsCache.InvalidateUserAsync(company.Id, user.Id, ct);
        return Result<object>.Success(new { });
    }
}

