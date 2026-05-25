using ERP.Application.Access.Caching;
using ERP.Application.Common;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Profiles;

public class GetProfilesHandler : IRequestHandler<GetProfilesQuery, Result<IReadOnlyList<ProfileDto>>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _tenant;

    public GetProfilesHandler(IAccessRepository repo, ICurrentSubscriber tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public Task<Result<IReadOnlyList<ProfileDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetProfilesQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<ProfileDto>>> Handle(GetProfilesQuery request, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var items = await _repo.GetProfilesBySubscriberAsync(subscriberId, request.OnlyActive, ct);
        return Result<IReadOnlyList<ProfileDto>>.Success(items
            .Select(p => new ProfileDto(p.Id, p.Name, p.Description, p.IsActive))
            .ToList());
    }
}

public class CreateProfileHandler : IRequestHandler<CreateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _user;

    public CreateProfileHandler(IAccessRepository repo, ICurrentSubscriber tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public Task<Result<ProfileDto>> HandleAsync(CreateProfileCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<ProfileDto>> Handle(CreateProfileCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name))
            return Result<ProfileDto>.Failure("Nombre requerido.");

        var subscriberId = _tenant.SubscriberId;
        var profile = AccessProfile.Create(subscriberId, cmd.Name, cmd.Description, _user.UserId);
        await _repo.AddProfileAsync(profile, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProfileDto>.Success(new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive));
    }
}

public class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _user;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public UpdateProfileHandler(
        IAccessRepository repo,
        ICurrentSubscriber tenant,
        ICurrentUser user,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
        _permissionsCache = permissionsCache;
    }

    public Task<Result<ProfileDto>> HandleAsync(UpdateProfileCommand cmd, CancellationToken ct = default)
        => Handle(cmd, ct);

    public async Task<Result<ProfileDto>> Handle(UpdateProfileCommand cmd, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var profile = await _repo.GetProfileByIdAsync(subscriberId, cmd.ProfileId, ct);
        if (profile is null)
            return Result<ProfileDto>.Failure("Perfil no encontrado.");

        profile.Update(cmd.Name, cmd.Description, _user.UserId);
        if (cmd.IsActive) profile.Activate(_user.UserId);
        else profile.Deactivate(_user.UserId);

        await _repo.SaveChangesAsync(ct);

        var memberships = await _repo.GetCompanyUserMembershipsBySubscriberAsync(subscriberId, onlyActive: true, ct);
        foreach (var companyId in memberships
                     .Where(m => m.ProfileId == cmd.ProfileId)
                     .Select(m => m.CompanyId)
                     .Distinct())
        {
            await _permissionsCache.BumpCompanyVersionAsync(companyId, ct);
        }

        return Result<ProfileDto>.Success(new ProfileDto(profile.Id, profile.Name, profile.Description, profile.IsActive));
    }
}

