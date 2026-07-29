using ERP.Application.Access.Caching;
using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// ADMIN READ MODEL — matriz CRUD de permisos asignados a un perfil (sin filtro de plan).
/// No usar para autorización runtime; ver <see cref="IEffectivePermissionKeysProvider"/>.
/// </summary>
[AdminReadModel("Matriz CRUD de permisos asignados a un perfil (sin filtro de plan).")]
public class GetProfilePermissionsHandler
    : IRequestHandler<GetProfilePermissionsQuery, Result<ProfilePermissionsDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProfilePermissionsHandler(IAccessRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public Task<Result<ProfilePermissionsDto>> HandleAsync(
        Guid profileId,
        CancellationToken cancellationToken = default
    ) => Handle(new GetProfilePermissionsQuery(profileId), cancellationToken);

    public async Task<Result<ProfilePermissionsDto>> Handle(
        GetProfilePermissionsQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!(_currentTenant.TenantId != Guid.Empty))
            return Result<ProfilePermissionsDto>.Failure("No autenticado.");

        var profile = await _repo.GetProfileByIdAsync(
            _currentTenant.TenantId,
            request.ProfileId,
            cancellationToken
        );
        if (profile is null)
            return Result<ProfilePermissionsDto>.Failure("Perfil no existe.");

        var perms = await _repo.GetProfilePermissionsAsync(
            _currentTenant.TenantId,
            request.ProfileId,
            cancellationToken
        );
        var items = perms
            .OrderBy(x => x.PermissionKey)
            .Select(x => new ProfilePermissionItemDto(x.PermissionKey, x.IsAllowed))
            .ToList();

        return Result<ProfilePermissionsDto>.Success(
            new ProfilePermissionsDto(request.ProfileId, items)
        );
    }
}
