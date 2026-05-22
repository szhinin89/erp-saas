using ERP.Application.Access.Caching;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class UpsertProfilePermissionsHandler : IRequestHandler<UpsertProfilePermissionsCommand, Result<object>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public UpsertProfilePermissionsHandler(
        IAccessRepository repo,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<object>> HandleAsync(UpsertProfilePermissionsCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<object>> Handle(UpsertProfilePermissionsCommand command, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated || !_currentUser.IsAuthenticated)
            return Result<object>.Failure("No autenticado.");

        if (command.Items is null || command.Items.Count == 0)
            return Result<object>.Failure("Debe enviar al menos 1 permiso.");

        var profile = await _repo.GetProfileByIdAsync(_currentSubscriber.SubscriberId, command.ProfileId, ct);
        if (profile is null)
            return Result<object>.Failure("Perfil no existe.");

        var actorId = _currentUser.UserId;

        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.PermissionKey))
                continue;

            var key = item.PermissionKey.Trim();
            var existing = await _repo.GetProfilePermissionAsync(_currentSubscriber.SubscriberId, command.ProfileId, key, ct);
            if (existing is null)
            {
                var created = AccessProfilePermission.Create(
                    subscriberId: _currentSubscriber.SubscriberId,
                    profileId: command.ProfileId,
                    permissionKey: key,
                    isAllowed: item.IsAllowed,
                    createdBy: actorId);
                await _repo.AddProfilePermissionAsync(created, ct);
            }
            else
            {
                existing.SetAllowed(item.IsAllowed, actorId);
            }
        }

        await _repo.SaveChangesAsync(ct);

        var memberships = await _repo.GetCompanyUserMembershipsBySubscriberAsync(_currentSubscriber.SubscriberId, onlyActive: true, ct);
        foreach (var companyId in memberships
                     .Where(m => m.ProfileId == command.ProfileId)
                     .Select(m => m.CompanyId)
                     .Distinct())
        {
            await _permissionsCache.BumpCompanyVersionAsync(companyId, ct);
        }

        return Result<object>.Success(new { });
    }
}

