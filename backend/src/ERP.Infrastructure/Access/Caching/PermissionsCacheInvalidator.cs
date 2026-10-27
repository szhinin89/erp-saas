using ERP.Application.Access.Caching;

namespace ERP.Infrastructure.Access.Caching;

public sealed class PermissionsCacheInvalidator : IPermissionsCacheInvalidator
{
    private readonly IPermissionsCacheBackend _cache;

    public PermissionsCacheInvalidator(IPermissionsCacheBackend cache)
    {
        _cache = cache;
    }

    public Task<bool> TryBeginAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    ) => _cache.TryBeginAsync(scope, cancellationToken);

    public Task<bool> TryCompleteAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    ) => _cache.TryCompleteAsync(scope, cancellationToken);
}
