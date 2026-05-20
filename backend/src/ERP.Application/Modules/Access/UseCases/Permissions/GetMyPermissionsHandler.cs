using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class GetMyPermissionsHandler : IRequestHandler<GetMyPermissionsQuery, Result<MyPermissionsDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantEntitlementsService _entitlements;

    public GetMyPermissionsHandler(
        IAccessRepository repo,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        ITenantEntitlementsService entitlements)
    {
        _repo = repo;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _entitlements = entitlements;
    }

    public Task<Result<MyPermissionsDto>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetMyPermissionsQuery(), ct);

    public async Task<Result<MyPermissionsDto>> Handle(GetMyPermissionsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || !_currentTenant.IsAuthenticated)
            return Result<MyPermissionsDto>.Failure("No autenticado.");

        var tenant = await _tenantRepository.GetByIdAsync(_currentTenant.TenantId, ct);
        var plan = tenant?.PlanCode;
        var modules = await TenantSubscriptionCatalog.ResolveEnabledModulesAsync(
            _currentTenant.TenantId, _entitlements, ct);

        var role = _currentUser.Role ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }, plan, modules));

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }, plan, modules));

        var membership = await _repo.GetMembershipAsync(_currentTenant.TenantId, _currentUser.UserId, ct);
        if (membership is null || !membership.IsActive)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        if (membership.ProfileId is null)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        var items = await _repo.GetProfilePermissionsAsync(_currentTenant.TenantId, membership.ProfileId.Value, ct);
        var allowed = items
            .Where(p => p.IsAllowed)
            .Select(p => p.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => tenant is null || TenantSubscriptionCatalog.TenantAllowsPermission(tenant, p))
            .OrderBy(x => x)
            .ToList();

        return Result<MyPermissionsDto>.Success(new MyPermissionsDto(allowed, plan, modules));
    }
}
