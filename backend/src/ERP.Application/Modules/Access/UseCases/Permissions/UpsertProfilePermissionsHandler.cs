using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class UpsertProfilePermissionsHandler
    : IRequestHandler<UpsertProfilePermissionsCommand, Result<PermissionUpsertResultDto>>
{
    private readonly IAccessRepository              _repo;
    private readonly ICurrentSubscriber             _currentSubscriber;
    private readonly ICurrentUser                   _currentUser;
    private readonly IPermissionsCacheInvalidator   _permissionsCache;
    private readonly IPermissionEntitlementValidator _validator;

    public UpsertProfilePermissionsHandler(
        IAccessRepository repo,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IPermissionsCacheInvalidator permissionsCache,
        IPermissionEntitlementValidator validator)
    {
        _repo              = repo;
        _currentSubscriber = currentSubscriber;
        _currentUser       = currentUser;
        _permissionsCache  = permissionsCache;
        _validator         = validator;
    }

    public Task<Result<PermissionUpsertResultDto>> HandleAsync(
        UpsertProfilePermissionsCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<PermissionUpsertResultDto>> Handle(
        UpsertProfilePermissionsCommand command, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated || !_currentUser.IsAuthenticated)
            return Result<PermissionUpsertResultDto>.Failure("No autenticado.");

        if (command.Items is null || command.Items.Count == 0)
            return Result<PermissionUpsertResultDto>.Failure("Debe enviar al menos 1 permiso.");

        var profile = await _repo.GetProfileByIdAsync(_currentSubscriber.SubscriberId, command.ProfileId, ct);
        if (profile is null)
            return Result<PermissionUpsertResultDto>.Failure("Perfil no existe.");

        var subscriberId = _currentSubscriber.SubscriberId;
        var actorId      = _currentUser.UserId;

        // Validate all submitted keys against the commercial plan BEFORE touching the DB.
        var keys            = command.Items.Where(i => !string.IsNullOrWhiteSpace(i.PermissionKey)).ToList();
        var validationResults = await _validator.ValidateAsync(
            subscriberId,
            keys.Select(i => i.PermissionKey),
            ct);

        var saved    = new List<string>();
        var rejected = new List<RejectedPermission>();

        foreach (var item in keys)
        {
            var key        = item.PermissionKey.Trim();
            var validation = validationResults.FirstOrDefault(r =>
                string.Equals(r.PermissionKey, key, StringComparison.OrdinalIgnoreCase));

            // Unknown-prefix permissions are flagged but still saved (current policy).
            if (validation?.Status == PermissionValidationStatus.BlockedByPlan)
            {
                rejected.Add(new RejectedPermission(
                    key,
                    validation.Reason ?? $"Módulo no incluido en el plan comercial.",
                    RejectionCodes.BlockedByPlan));
                continue;
            }

            // Save/update the permission
            var existing = await _repo.GetProfilePermissionAsync(subscriberId, command.ProfileId, key, ct);
            if (existing is null)
            {
                var created = AccessProfilePermission.Create(
                    subscriberId:  subscriberId,
                    profileId:     command.ProfileId,
                    permissionKey: key,
                    isAllowed:     item.IsAllowed,
                    createdBy:     actorId);
                await _repo.AddProfilePermissionAsync(created, ct);
            }
            else
            {
                existing.SetAllowed(item.IsAllowed, actorId);
            }

            if (validation?.Status == PermissionValidationStatus.UnknownPrefix)
            {
                // Saved but flagged — include in saved with a note via rejected list
                saved.Add(key);
                rejected.Add(new RejectedPermission(
                    key,
                    validation.Reason ?? "Prefijo desconocido — no gobernado por plan.",
                    RejectionCodes.UnknownPrefix));
            }
            else
            {
                saved.Add(key);
            }
        }

        await _repo.SaveChangesAsync(ct);

        // Invalidate effective permissions cache for all users assigned to this profile.
        var memberships = await _repo.GetCompanyUserMembershipsBySubscriberAsync(
            subscriberId, onlyActive: true, ct);

        foreach (var companyId in memberships
            .Where(m => m.ProfileId == command.ProfileId)
            .Select(m => m.CompanyId)
            .Distinct())
        {
            await _permissionsCache.BumpCompanyVersionAsync(companyId, ct);
        }

        return Result<PermissionUpsertResultDto>.Success(
            new PermissionUpsertResultDto(saved, rejected));
    }
}
