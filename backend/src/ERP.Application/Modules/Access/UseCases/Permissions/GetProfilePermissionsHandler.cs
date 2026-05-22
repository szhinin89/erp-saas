using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

/// <summary>
/// ADMIN READ MODEL — matriz CRUD de permisos asignados a un perfil (sin filtro de plan).
/// No usar para autorización runtime; ver <see cref="IEffectivePermissionKeysProvider"/>.
/// </summary>
[AdminReadModel("Matriz CRUD de permisos asignados a un perfil (sin filtro de plan).")]
public class GetProfilePermissionsHandler : IRequestHandler<GetProfilePermissionsQuery, Result<ProfilePermissionsDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProfilePermissionsHandler(IAccessRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<ProfilePermissionsDto>> HandleAsync(Guid profileId, CancellationToken ct = default)
        => Handle(new GetProfilePermissionsQuery(profileId), ct);

    public async Task<Result<ProfilePermissionsDto>> Handle(GetProfilePermissionsQuery request, CancellationToken ct)
    {
        if (!_currentSubscriber.IsAuthenticated)
            return Result<ProfilePermissionsDto>.Failure("No autenticado.");

        var profile = await _repo.GetProfileByIdAsync(_currentSubscriber.SubscriberId, request.ProfileId, ct);
        if (profile is null)
            return Result<ProfilePermissionsDto>.Failure("Perfil no existe.");

        var perms = await _repo.GetProfilePermissionsAsync(_currentSubscriber.SubscriberId, request.ProfileId, ct);
        var items = perms
            .OrderBy(x => x.PermissionKey)
            .Select(x => new ProfilePermissionItemDto(x.PermissionKey, x.IsAllowed))
            .ToList();

        return Result<ProfilePermissionsDto>.Success(new ProfilePermissionsDto(request.ProfileId, items));
    }
}
