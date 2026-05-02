using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.UpsertMembership;

public class UpsertMembershipHandler
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;

    public UpsertMembershipHandler(
        IAccessRepository accessRepository,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment)
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
        _deployment = deployment;
    }

    public async Task<Result<object>> HandleAsync(UpsertMembershipCommand command, CancellationToken ct = default)
    {
        if (string.Equals(command.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return Result<object>.Failure(
                "Solo puede existir un SuperAdmin por servidor (tabla users). No se asigna por membresía IAM.");
        }

        var email = command.UserEmail.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var existing = await _accessRepository.GetMembershipAsync(command.TenantId, user.Id, ct);
        if (existing is null)
        {
            var cap = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(
                _deployment, _accessRepository, command.TenantId, ct);
            if (cap is not null)
                return Result<object>.Failure(cap);

            var membership = Membership.Create(command.TenantId, user.Id, command.Role, command.ProfileId, createdBy: _currentUser.UserId);
            await _accessRepository.AddMembershipAsync(membership, ct);
        }
        else
        {
            if (!existing.IsActive)
            {
                var capRe = await DeploymentQuota.GetBlockingReasonIfAtTenantMembershipUserCapAsync(
                    _deployment, _accessRepository, command.TenantId, ct);
                if (capRe is not null)
                    return Result<object>.Failure(capRe);
            }

            existing.Activate(command.Role, command.ProfileId, updatedBy: _currentUser.UserId);
        }

        await _accessRepository.SaveChangesAsync(ct);
        return Result<object>.Success(new { });
    }
}

