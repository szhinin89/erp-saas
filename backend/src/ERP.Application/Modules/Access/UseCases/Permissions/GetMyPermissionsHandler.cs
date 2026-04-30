using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class GetMyPermissionsHandler
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public GetMyPermissionsHandler(
        IAccessRepository repo,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public async Task<Result<MyPermissionsDto>> HandleAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentTenant.IsAuthenticated)
            return Result<MyPermissionsDto>.Failure("No autenticado.");

        var role = _currentUser.Role ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }));

        // Admin del tenant: por defecto acceso total dentro del tenant (se puede endurecer luego).
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }));

        // Para roles no-admin: permisos por ProfileId de la membership del usuario en el tenant actual.
        var membership = await _repo.GetMembershipAsync(_currentTenant.TenantId, _currentUser.UserId, ct);
        if (membership is null || !membership.IsActive)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>()));

        if (membership.ProfileId is null)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>()));

        var items = await _repo.GetProfilePermissionsAsync(_currentTenant.TenantId, membership.ProfileId.Value, ct);
        var allowed = items
            .Where(p => p.IsAllowed)
            .Select(p => p.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        return Result<MyPermissionsDto>.Success(new MyPermissionsDto(allowed));
    }
}

