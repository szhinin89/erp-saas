using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Profiles;

public class GetProfilesHandler
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _tenant;

    public GetProfilesHandler(IAccessRepository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<ProfileDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        var items = await _repo.GetProfilesByTenantAsync(tenantId, onlyActive, ct);
        return Result<IReadOnlyList<ProfileDto>>.Success(items
            .Select(p => new ProfileDto(p.Id, p.Name, p.Description, p.IsActive))
            .ToList());
    }
}

public class CreateProfileHandler
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreateProfileHandler(IAccessRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<ProfileDto>> HandleAsync(CreateProfileCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return Result<ProfileDto>.Failure("Nombre requerido.");

        var tenantId = _tenant.TenantId;
        var profile = AccessProfile.Create(tenantId, cmd.Name, cmd.Description, _user.UserId);
        await _repo.AddProfileAsync(profile, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProfileDto>.Success(new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive));
    }
}

public class UpdateProfileHandler
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public UpdateProfileHandler(IAccessRepository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<ProfileDto>> HandleAsync(UpdateProfileCommand cmd, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        var profile = await _repo.GetProfileByIdAsync(tenantId, cmd.ProfileId, ct);
        if (profile is null)
            return Result<ProfileDto>.Failure("Perfil no encontrado.");

        profile.Update(cmd.Name, cmd.Description, _user.UserId);
        if (cmd.IsActive) profile.Activate(_user.UserId);
        else profile.Deactivate(_user.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<ProfileDto>.Success(new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive));
    }
}

