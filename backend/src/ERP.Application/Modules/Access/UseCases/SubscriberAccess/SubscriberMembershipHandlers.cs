using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.SubscriberAccess;

public class GetSubscriberCompanyUserMembershipsHandler : IRequestHandler<GetSubscriberCompanyUserMembershipsQuery, Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _subscriber;

    public GetSubscriberCompanyUserMembershipsHandler(IAccessRepository repo, ICurrentSubscriber subscriber)
    {
        _repo = repo;
        _subscriber = subscriber;
    }

    public Task<Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetSubscriberCompanyUserMembershipsQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<SubscriberCompanyUserMembershipItemDto>>> Handle(GetSubscriberCompanyUserMembershipsQuery request, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var company_user_memberships = await _repo.GetCompanyUserMembershipsBySubscriberAsync(subscriberId, request.OnlyActive, ct);

        // En este MVP, solo retornamos company_user_memberships. Los lines del usuario se leen por Id (sin joins complejos).
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
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentCompany _currentCompany;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public SubscriberUpsertCompanyUserMembershipHandler(
        IAccessRepository repo,
        ICurrentSubscriber subscriber,
        ICurrentUser currentUser,
        ICurrentCompany currentCompany,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher,
        ICompanyProvisioningService companyProvisioning,
        ISubscriberRepository subscriberRepository,
        ICompanyRepository companyRepository,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _repo = repo;
        _subscriber = subscriber;
        _currentUser = currentUser;
        _currentCompany = currentCompany;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
        _companyProvisioning = companyProvisioning;
        _subscriberRepository = subscriberRepository;
        _companyRepository = companyRepository;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(SubscriberUpsertCompanyUserMembershipCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<object>> Handle(SubscriberUpsertCompanyUserMembershipCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
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

        // Prefer the company the calling admin is currently operating in (from JWT company_id claim).
        // Falling back to EnsureDefaultCompanyAsync picks active[0] which may be a provisional company
        // when the subscriber has multiple companies (e.g., one onboarded + several provisional stubs).
        Company company;
        var sessionCompanyId = _currentCompany.CompanyId;
        if (sessionCompanyId != Guid.Empty)
        {
            var sessionCompany = await _companyRepository.GetByIdAsync(sessionCompanyId, ct);
            company = sessionCompany ?? await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        }
        else
        {
            company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        }

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
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public SubscriberRevokeCompanyUserMembershipHandler(
        IAccessRepository repo,
        ICurrentSubscriber subscriber,
        ICurrentUser currentUser,
        ICurrentCompany currentCompany,
        ICompanyProvisioningService companyProvisioning,
        ISubscriberRepository subscriberRepository,
        ICompanyRepository companyRepository,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _repo = repo;
        _subscriber = subscriber;
        _currentUser = currentUser;
        _currentCompany = currentCompany;
        _companyProvisioning = companyProvisioning;
        _subscriberRepository = subscriberRepository;
        _companyRepository = companyRepository;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(SubscriberRevokeCompanyUserMembershipCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<object>> Handle(SubscriberRevokeCompanyUserMembershipCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Success(new { });

        var tenant = await _subscriberRepository.GetByIdAsync(subscriberId, ct);
        if (tenant is null)
            return Result<object>.Success(new { });

        Company company;
        var sessionCompanyId = _currentCompany.CompanyId;
        if (sessionCompanyId != Guid.Empty)
        {
            var sessionCompany = await _companyRepository.GetByIdAsync(sessionCompanyId, ct);
            company = sessionCompany ?? await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        }
        else
        {
            company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
        }

        var membership = await _repo.GetCompanyUserMembershipAsync(company.Id, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<object>.Success(new { });

        membership.Deactivate(_currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        await _permissionsCache.InvalidateUserAsync(company.Id, user.Id, ct);
        return Result<object>.Success(new { });
    }
}

