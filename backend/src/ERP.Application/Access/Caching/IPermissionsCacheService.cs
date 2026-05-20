namespace ERP.Application.Access.Caching;

public interface IPermissionsCacheService
{
    Task<IReadOnlyList<string>?> GetPermissionKeysAsync(Guid companyId, Guid userId, CancellationToken ct = default);

    Task SetPermissionKeysAsync(Guid companyId, Guid userId, IReadOnlyList<string> keys, TimeSpan? ttl = null, CancellationToken ct = default);

    Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken ct = default);

    Task InvalidateCompanyAsync(Guid companyId, CancellationToken ct = default);
}
