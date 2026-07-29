namespace ERP.Application.Access.Caching;

/// <summary>
/// Invalidación centralizada del cache de permisos efectivos (write-side exclusivo).
/// Los handlers de mutación deben usar esta abstracción — nunca <see cref="IPermissionsCacheService"/>.
/// </summary>
public interface IPermissionsCacheInvalidator
{
    Task InvalidateUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task BumpCompanyVersionAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task BumpTenantVersionAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
