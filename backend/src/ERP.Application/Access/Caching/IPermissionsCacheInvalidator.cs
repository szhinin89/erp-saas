namespace ERP.Application.Access.Caching;

/// <summary>
/// Invalidación centralizada del cache de permisos efectivos (write-side exclusivo).
/// Los handlers de mutación deben usar esta abstracción — nunca <see cref="IPermissionsCacheService"/>.
/// </summary>
public interface IPermissionsCacheInvalidator
{
    Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken ct = default);

    Task BumpCompanyVersionAsync(Guid companyId, CancellationToken ct = default);

    Task BumpSubscriberVersionAsync(Guid subscriberId, CancellationToken ct = default);
}
