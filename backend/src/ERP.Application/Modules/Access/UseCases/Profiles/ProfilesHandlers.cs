using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Profiles;

public class GetProfilesHandler : IRequestHandler<GetProfilesQuery, Result<IReadOnlyList<ProfileDto>>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProfilesHandler(IAccessRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _currentTenant = tenant;
    }

    public Task<Result<IReadOnlyList<ProfileDto>>> HandleAsync(bool onlyActive, CancellationToken cancellationToken = default)
        => Handle(new GetProfilesQuery(onlyActive), cancellationToken);

    public async Task<Result<IReadOnlyList<ProfileDto>>> Handle(GetProfilesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProfilesByTenantAsync(tenantId, request.OnlyActive, cancellationToken);
        return Result<IReadOnlyList<ProfileDto>>.Success(items
            .Select(p => new ProfileDto(p.Id, p.Name, p.Description, p.IsActive))
            .ToList());
    }
}

public class CreateProfileHandler : IRequestHandler<CreateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public CreateProfileHandler(IAccessRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
    }

    public Task<Result<ProfileDto>> HandleAsync(CreateProfileCommand cmd, CancellationToken cancellationToken = default)
        => Handle(cmd, cancellationToken);

    public async Task<Result<ProfileDto>> Handle(CreateProfileCommand cmd, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return Result<ProfileDto>.Failure("Nombre requerido.");

        var tenantId = _currentTenant.TenantId;
        var profile = AccessProfile.Create(tenantId, cmd.Name, cmd.Description, _user.UserId);
        await _repo.AddProfileAsync(profile, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<ProfileDto>.Success(new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive));
    }
}

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder _navigationBuilder;

    public UpdateProfileHandler(
        IAccessRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder)
    {
        _repo = repo;
        _currentTenant = tenant;
        _user = user;
        _permissionsCache = permissionsCache;
        _navigationBuilder = navigationBuilder;
    }

    public Task<Result<ProfileDto>> HandleAsync(UpdateProfileCommand cmd, CancellationToken cancellationToken = default)
        => Handle(cmd, cancellationToken);

    public async Task<Result<ProfileDto>> Handle(UpdateProfileCommand cmd, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var profile = await _repo.GetProfileByIdAsync(tenantId, cmd.ProfileId, cancellationToken);
        if (profile is null)
            return Result<ProfileDto>.Failure("Perfil no encontrado.");

        profile.Update(cmd.Name, cmd.Description, _user.UserId);
        if (cmd.IsActive) profile.Activate(_user.UserId);
        else profile.Deactivate(_user.UserId);

        await _repo.SaveChangesAsync(cancellationToken);

        var memberships = await _repo.GetCompanyUserMembershipsByTenantAsync(tenantId, onlyActive: true, cancellationToken);
        var profileMemberships = memberships
            .Where(m => m.ProfileId == cmd.ProfileId)
            .ToList();

        foreach (var companyId in profileMemberships
                     .Select(m => m.CompanyId)
                     .Distinct())
        {
            await _permissionsCache.BumpCompanyVersionAsync(companyId, cancellationToken);
        }

        foreach (var membership in profileMemberships)
        {
            _navigationBuilder.InvalidateCache(tenantId, membership.CompanyId, membership.IdentityUserId);
        }

        return Result<ProfileDto>.Success(new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive));
    }
}

