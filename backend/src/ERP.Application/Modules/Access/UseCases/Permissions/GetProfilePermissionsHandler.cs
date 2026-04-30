using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class GetProfilePermissionsHandler
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProfilePermissionsHandler(IAccessRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ProfilePermissionsDto>> HandleAsync(Guid profileId, CancellationToken ct = default)
    {
        if (!_currentTenant.IsAuthenticated)
            return Result<ProfilePermissionsDto>.Failure("No autenticado.");

        var profile = await _repo.GetProfileByIdAsync(_currentTenant.TenantId, profileId, ct);
        if (profile is null)
            return Result<ProfilePermissionsDto>.Failure("Perfil no existe.");

        var perms = await _repo.GetProfilePermissionsAsync(_currentTenant.TenantId, profileId, ct);
        var items = perms
            .OrderBy(x => x.PermissionKey)
            .Select(x => new ProfilePermissionItemDto(x.PermissionKey, x.IsAllowed))
            .ToList();

        return Result<ProfilePermissionsDto>.Success(new ProfilePermissionsDto(profileId, items));
    }
}

