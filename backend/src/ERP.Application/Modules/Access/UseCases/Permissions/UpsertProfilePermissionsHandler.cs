using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class UpsertProfilePermissionsHandler : IRequestHandler<UpsertProfilePermissionsCommand, Result<object>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpsertProfilePermissionsHandler(
        IAccessRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public Task<Result<object>> HandleAsync(UpsertProfilePermissionsCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<object>> Handle(UpsertProfilePermissionsCommand command, CancellationToken ct)
    {
        if (!_currentTenant.IsAuthenticated || !_currentUser.IsAuthenticated)
            return Result<object>.Failure("No autenticado.");

        if (command.Items is null || command.Items.Count == 0)
            return Result<object>.Failure("Debe enviar al menos 1 permiso.");

        var profile = await _repo.GetProfileByIdAsync(_currentTenant.TenantId, command.ProfileId, ct);
        if (profile is null)
            return Result<object>.Failure("Perfil no existe.");

        var actorId = _currentUser.UserId;

        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.PermissionKey))
                continue;

            var key = item.PermissionKey.Trim();
            var existing = await _repo.GetProfilePermissionAsync(_currentTenant.TenantId, command.ProfileId, key, ct);
            if (existing is null)
            {
                var created = AccessProfilePermission.Create(
                    tenantId: _currentTenant.TenantId,
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
        return Result<object>.Success(new { });
    }
}

