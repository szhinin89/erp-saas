using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class GetProfilePermissionsHandler : IRequestHandler<GetProfilePermissionsQuery, Result<ProfilePermissionsDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProfilePermissionsHandler(IAccessRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public Task<Result<ProfilePermissionsDto>> HandleAsync(Guid profileId, CancellationToken ct = default)
        => Handle(new GetProfilePermissionsQuery(profileId), ct);

    public async Task<Result<ProfilePermissionsDto>> Handle(GetProfilePermissionsQuery request, CancellationToken ct)
    {
        if (!_currentTenant.IsAuthenticated)
            return Result<ProfilePermissionsDto>.Failure("No autenticado.");

        var profile = await _repo.GetProfileByIdAsync(_currentTenant.TenantId, request.ProfileId, ct);
        if (profile is null)
            return Result<ProfilePermissionsDto>.Failure("Perfil no existe.");

        var perms = await _repo.GetProfilePermissionsAsync(_currentTenant.TenantId, request.ProfileId, ct);
        var items = perms
            .OrderBy(x => x.PermissionKey)
            .Select(x => new ProfilePermissionItemDto(x.PermissionKey, x.IsAllowed))
            .ToList();

        return Result<ProfilePermissionsDto>.Success(new ProfilePermissionsDto(request.ProfileId, items));
    }
}

