using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Navigation;
using MediatR;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class UpsertProfilePermissionsHandler
    : IRequestHandler<UpsertProfilePermissionsCommand, Result<PermissionUpsertResultDto>>
{
    private readonly IAccessRepository            _repo;
    private readonly ICurrentTenant           _currentTenant;
    private readonly ICurrentUser                 _currentUser;
    private readonly IPermissionsCacheInvalidator _permissionsCache;
    private readonly INavigationBuilder           _navigationBuilder;

    public UpsertProfilePermissionsHandler(
        IAccessRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IPermissionsCacheInvalidator permissionsCache,
        INavigationBuilder navigationBuilder)
    {
        _repo              = repo;
        _currentTenant = currentTenant;
        _currentUser       = currentUser;
        _permissionsCache  = permissionsCache;
        _navigationBuilder = navigationBuilder;
    }

    public Task<Result<PermissionUpsertResultDto>> HandleAsync(
        UpsertProfilePermissionsCommand command, CancellationToken cancellationToken = default)
        => Handle(command, cancellationToken);

    public async Task<Result<PermissionUpsertResultDto>> Handle(
        UpsertProfilePermissionsCommand command, CancellationToken cancellationToken)
    {
        if (!(_currentTenant.TenantId != Guid.Empty) || !_currentUser.IsAuthenticated)
            return Result<PermissionUpsertResultDto>.Failure("No autenticado.");

        if (command.Items is null || command.Items.Count == 0)
            return Result<PermissionUpsertResultDto>.Failure("Debe enviar al menos 1 permiso.");

        var profile = await _repo.GetProfileByIdAsync(_currentTenant.TenantId, command.ProfileId, cancellationToken);
        if (profile is null)
            return Result<PermissionUpsertResultDto>.Failure("Perfil no existe.");

        var tenantId = _currentTenant.TenantId;
        var actorId      = _currentUser.UserId;
        var saved        = new List<string>();
        var rejected     = new List<RejectedPermission>();

        foreach (var item in command.Items.Where(i => !string.IsNullOrWhiteSpace(i.PermissionKey)))
        {
            var key      = item.PermissionKey.Trim();
            var existing = await _repo.GetProfilePermissionAsync(tenantId, command.ProfileId, key, cancellationToken);

            if (existing is null)
            {
                var created = AccessProfilePermission.Create(
                    tenantId:  tenantId,
                    profileId:     command.ProfileId,
                    permissionKey: key,
                    isAllowed:     item.IsAllowed,
                    createdBy:     actorId);
                await _repo.AddProfilePermissionAsync(created, cancellationToken);
            }
            else
            {
                existing.SetAllowed(item.IsAllowed, actorId);
            }

            saved.Add(key);
        }

        await _repo.SaveChangesAsync(cancellationToken);

        var memberships = await _repo.GetCompanyUserMembershipsByTenantAsync(
            tenantId, onlyActive: true, cancellationToken);

        var profileMemberships = memberships
            .Where(m => m.ProfileId == command.ProfileId)
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

        return Result<PermissionUpsertResultDto>.Success(new PermissionUpsertResultDto(saved, rejected));
    }
}
