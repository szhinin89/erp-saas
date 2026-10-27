using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.Profiles;

// SEC-05: todo perfil se lee y escribe en el tenant y la empresa activa, ya validados por
// CompanyScopeBehavior (IRequiresCompanyContext); nunca por un id del cliente.

public class GetProfilesHandler
    : IRequestHandler<GetProfilesQuery, Result<IReadOnlyList<ProfileDto>>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetProfilesHandler(
        IAccessRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany currentCompany
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _currentCompany = currentCompany;
    }

    public Task<Result<IReadOnlyList<ProfileDto>>> HandleAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default
    ) => Handle(new GetProfilesQuery(onlyActive), cancellationToken);

    public async Task<Result<IReadOnlyList<ProfileDto>>> Handle(
        GetProfilesQuery request,
        CancellationToken cancellationToken
    )
    {
        var items = await _repo.GetProfilesByCompanyAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            request.OnlyActive,
            cancellationToken
        );
        return Result<IReadOnlyList<ProfileDto>>.Success(
            items.Select(p => new ProfileDto(p.Id, p.Name, p.Description, p.IsActive)).ToList()
        );
    }
}

public class CreateProfileHandler : IRequestHandler<CreateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _user;

    public CreateProfileHandler(
        IAccessRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany currentCompany,
        ICurrentUser user
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _currentCompany = currentCompany;
        _user = user;
    }

    public Task<Result<ProfileDto>> HandleAsync(
        CreateProfileCommand cmd,
        CancellationToken cancellationToken = default
    ) => Handle(cmd, cancellationToken);

    public async Task<Result<ProfileDto>> Handle(
        CreateProfileCommand cmd,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return Result<ProfileDto>.Failure("Nombre requerido.");

        var profile = AccessProfile.Create(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            cmd.Name,
            cmd.Description,
            _user.UserId
        );
        await _repo.AddProfileAsync(profile, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<ProfileDto>.Success(
            new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive)
        );
    }
}

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _user;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder _navigationBuilder;

    public UpdateProfileHandler(
        IAccessRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany currentCompany,
        ICurrentUser user,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder
    )
    {
        _repo = repo;
        _currentTenant = tenant;
        _currentCompany = currentCompany;
        _user = user;
        _permissionsCache = permissionsCache;
        _navigationBuilder = navigationBuilder;
    }

    public Task<Result<ProfileDto>> HandleAsync(
        UpdateProfileCommand cmd,
        CancellationToken cancellationToken = default
    ) => Handle(cmd, cancellationToken);

    public async Task<Result<ProfileDto>> Handle(
        UpdateProfileCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var profile = await _repo.GetProfileByIdAsync(
            tenantId,
            companyId,
            cmd.ProfileId,
            cancellationToken
        );
        if (profile is null)
            return Result<ProfileDto>.Failure("Perfil no encontrado.");

        profile.Update(cmd.Name, cmd.Description, _user.UserId);
        if (cmd.IsActive)
            profile.Activate(_user.UserId);
        else
            profile.Deactivate(_user.UserId);

        await _repo.SaveChangesAsync(cancellationToken);

        // SEC-05: el perfil es de esta empresa, así que solo sus memberships cambian de permisos
        // (incluida la desactivación, que deja de conceder permisos efectivos).
        await ProfileCacheInvalidation.InvalidateAsync(
            _repo,
            _permissionsCache,
            _navigationBuilder,
            tenantId,
            companyId,
            cmd.ProfileId,
            cancellationToken
        );

        return Result<ProfileDto>.Success(
            new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive)
        );
    }
}

/// <summary>
/// Invalidación tras cambiar un perfil (datos o permisos): versión de permisos de la empresa y
/// navegación de cada usuario con ese perfil en ella. Compartida por UpdateProfile y
/// UpsertProfilePermissions — un solo punto, sin mecanismos paralelos.
/// </summary>
internal static class ProfileCacheInvalidation
{
    public static async Task InvalidateAsync(
        IAccessRepository repo,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder,
        Guid tenantId,
        Guid companyId,
        Guid profileId,
        CancellationToken cancellationToken
    )
    {
        var profileMemberships = (
            await repo.GetCompanyUserMembershipsByCompanyAsync(
                companyId,
                onlyActive: true,
                cancellationToken
            )
        )
            .Where(m => m.ProfileId == profileId)
            .ToList();
        if (profileMemberships.Count == 0)
            return;

        await permissionsCache.BumpCompanyVersionAsync(companyId, cancellationToken);

        foreach (var membership in profileMemberships)
            navigationBuilder.InvalidateCache(tenantId, companyId, membership.IdentityUserId);
    }
}
