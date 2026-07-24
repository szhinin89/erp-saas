namespace ERP.Application.Access.Caching;

/// <summary>
/// Cache-aside de permisos efectivos (read/write). Solo lo consume <see cref="IEffectivePermissionKeysProvider"/>.
/// Invalidación write-side: <see cref="IPermissionsCacheInvalidator"/>.
/// </summary>
public interface IPermissionsCacheService
{
    Task<PermissionsCacheReadResult> ReadAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        IReadOnlyList<string> keys,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);
}
