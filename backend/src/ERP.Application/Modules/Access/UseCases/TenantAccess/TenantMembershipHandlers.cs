using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.TenantAccess;

public class GetTenantMembershipsHandler : IRequestHandler<GetTenantMembershipsQuery, Result<IReadOnlyList<TenantMembershipItemDto>>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _tenant;

    public GetTenantMembershipsHandler(IAccessRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public Task<Result<IReadOnlyList<TenantMembershipItemDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetTenantMembershipsQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<TenantMembershipItemDto>>> Handle(GetTenantMembershipsQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var memberships = await _repo.GetMembershipsByTenantAsync(tenantId, request.OnlyActive, ct);

        // En este MVP, solo retornamos memberships. Los detalles del usuario se leen por Id (sin joins complejos).
        // Para UX, hacemos lookup por email/nombre desde IdentityUsers.
        var users = new Dictionary<Guid, IdentityUser>();
        foreach (var m in memberships)
        {
            if (users.ContainsKey(m.IdentityUserId)) continue;
            var u = await _repo.GetUserByIdAsync(m.IdentityUserId, ct);
            if (u is not null) users[m.IdentityUserId] = u;
        }

        var items = memberships.Select(m =>
        {
            users.TryGetValue(m.IdentityUserId, out var u);
            return new TenantMembershipItemDto(
                IdentityUserId: m.IdentityUserId,
                Email: u?.Email.Value ?? "",
                FullName: u?.FullName ?? "",
                Role: m.Role,
                ProfileId: m.ProfileId,
                IsActive: m.IsActive
            );
        }).ToList();

        return Result<IReadOnlyList<TenantMembershipItemDto>>.Success(items);
    }
}

public class TenantUpsertMembershipHandler : IRequestHandler<TenantUpsertMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IPasswordHasher _passwordHasher;

    public TenantUpsertMembershipHandler(
        IAccessRepository repo,
        ICurrentTenant tenant,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        IPasswordHasher passwordHasher)
    {
        _repo = repo;
        _tenant = tenant;
        _currentUser = currentUser;
        _deployment = deployment;
        _passwordHasher = passwordHasher;
    }

    public Task<Result<object>> HandleAsync(TenantUpsertMembershipCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<object>> Handle(TenantUpsertMembershipCommand cmd, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId == Guid.Empty)
            return Result<object>.Failure("Tenant inválido.");

        if (string.Equals(cmd.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return Result<object>.Failure(
                "Solo puede existir un SuperAdmin por servidor (tabla users). No se asigna por membresía IAM.");
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

        var membership = await _repo.GetMembershipAsync(tenantId, user.Id, ct);
        if (membership is null)
        {
            var cap = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(_deployment, _repo, tenantId, ct);
            if (cap is not null)
                return Result<object>.Failure(cap);

            membership = Membership.Create(tenantId, user.Id, cmd.Role, cmd.ProfileId, _currentUser.UserId);
            await _repo.AddMembershipAsync(membership, ct);
        }
        else
        {
            if (!membership.IsActive)
            {
                var capRe = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(_deployment, _repo, tenantId, ct);
                if (capRe is not null)
                    return Result<object>.Failure(capRe);
            }

            membership.Activate(cmd.Role, cmd.ProfileId, _currentUser.UserId);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<object>.Success(new { });
    }
}

public class TenantRevokeMembershipHandler : IRequestHandler<TenantRevokeMembershipCommand, Result<object>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _currentUser;

    public TenantRevokeMembershipHandler(IAccessRepository repo, ICurrentTenant tenant, ICurrentUser currentUser)
    {
        _repo = repo;
        _tenant = tenant;
        _currentUser = currentUser;
    }

    public Task<Result<object>> HandleAsync(TenantRevokeMembershipCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<object>> Handle(TenantRevokeMembershipCommand cmd, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await _repo.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Success(new { });

        var membership = await _repo.GetMembershipAsync(tenantId, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<object>.Success(new { });

        membership.Deactivate(_currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<object>.Success(new { });
    }
}

