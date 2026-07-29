using ERP.Application.Access.Caching;
using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Kernel.Security;
using MediatR;

namespace ERP.Application.Access.UseCases.Permissions;

public class GetMyPermissionsHandler
    : IRequestHandler<GetMyPermissionsQuery, Result<MyPermissionsDto>>
{
    private static readonly string[] AdminPermissions = ["*"];
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEffectivePermissionKeysProvider _permissionKeys;
    private readonly ICompanyContextProvider _companyContext;

    public GetMyPermissionsHandler(
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IEffectivePermissionKeysProvider permissionKeys,
        ICompanyContextProvider companyContext
    )
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _permissionKeys = permissionKeys;
        _companyContext = companyContext;
    }

    public Task<Result<MyPermissionsDto>> HandleAsync(
        CancellationToken cancellationToken = default
    ) => Handle(new GetMyPermissionsQuery(), cancellationToken);

    public async Task<Result<MyPermissionsDto>> Handle(
        GetMyPermissionsQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated || _currentTenant.TenantId == Guid.Empty)
            return Result<MyPermissionsDto>.Failure("No autenticado.");

        var role = _currentUser.Role ?? string.Empty;
        if (string.Equals(role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(AdminPermissions));

        var context = await _companyContext.ResolveOperationalForCurrentUserAsync(
            cancellationToken
        );
        if (context is null || context.CompanyId == Guid.Empty)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto([]));

        if (!context.IsActiveMembership || context.ProfileId is null)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto([]));

        var allowed = await _permissionKeys.GetAllowedKeysAsync(
            _currentTenant.TenantId,
            context.CompanyId,
            _currentUser.UserId,
            context.ProfileId.Value,
            cancellationToken
        );

        return Result<MyPermissionsDto>.Success(new MyPermissionsDto(allowed));
    }
}
