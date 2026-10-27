namespace ERP.Application.Access.Caching;

/// <summary>
/// Cache-aside de permisos efectivos (read/write). Solo lo consume <see cref="IEffectivePermissionKeysProvider"/>.
/// Invalidación write-side: <see cref="IPermissionsCacheInvalidator"/>.
/// <para>
/// SEC-06: <see cref="ReadAsync"/> observa las versiones ANTES de que el consumidor cargue la BD y las
/// devuelve en <see cref="PermissionsCacheReadResult.Stamp"/>; <see cref="WriteAsync"/> publica la
/// entrada con ese sello, nunca con la versión vigente al escribir. Una carga iniciada antes de una
/// invalidación nace obsoleta. La entrada lleva además el perfil para el que se calculó.
/// </para>
/// </summary>
public interface IPermissionsCacheService
{
    Task<PermissionsCacheReadResult> ReadAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        CancellationToken cancellationToken = default
    );

    Task WriteAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        IReadOnlyList<string> keys,
        PermissionsCacheStamp stamp,
        CancellationToken cancellationToken = default
    );
}
