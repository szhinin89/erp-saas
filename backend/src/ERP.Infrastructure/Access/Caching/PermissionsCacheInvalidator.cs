using ERP.Application.Access.Caching;

namespace ERP.Infrastructure.Access.Caching;

public sealed class PermissionsCacheInvalidator : IPermissionsCacheInvalidator
{
    private readonly IPermissionsCacheBackend _cache;

    public PermissionsCacheInvalidator(IPermissionsCacheBackend cache)
    {
        _cache = cache;
    }

    public Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken ct = default)
        => _cache.InvalidateUserAsync(companyId, userId, ct);

    public Task BumpCompanyVersionAsync(Guid companyId, CancellationToken ct = default)
        => _cache.BumpCompanyVersionAsync(companyId, ct);

    public Task BumpSubscriberVersionAsync(Guid subscriberId, CancellationToken ct = default)
        => _cache.BumpSubscriberVersionAsync(subscriberId, ct);
}
