using ERP.Application.Access.Authorization;

namespace ERP.API.Tests.Support;

internal sealed class AllowAllPermissionAuthorizer : IRuntimePermissionAuthorizer
{
    public Task<bool> IsAuthorizedAsync(
        string permissionKey,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}