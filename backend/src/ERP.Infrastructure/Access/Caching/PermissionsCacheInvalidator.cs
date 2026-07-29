using ERP.Application.Access.Caching;

namespace ERP.Infrastructure.Access.Caching;

public sealed class PermissionsCacheInvalidator : IPermissionsCacheInvalidator
{
    private readonly IPermissionsCacheBackend _cache;

    public PermissionsCacheInvalidator(IPermissionsCacheBackend cache)
    {
        _cache = cache;
    }

    public Task InvalidateUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default
    ) => _cache.InvalidateUserAsync(companyId, userId, cancellationToken);

    public Task BumpCompanyVersionAsync(
        Guid companyId,
        CancellationToken cancellationToken = default
    ) => _cache.BumpCompanyVersionAsync(companyId, cancellationToken);

    public Task BumpTenantVersionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) => _cache.BumpTenantVersionAsync(tenantId, cancellationToken);
}
