namespace ERP.Application.Access.Authorization;

/// <summary>
/// Autorización runtime de permisos (<c>perm:*</c>).
/// Única autoridad de decisión en Application: delega contexto a <see cref="ICompanyContextProvider"/>
/// y claves efectivas a <see cref="Caching.IEffectivePermissionKeysProvider"/>.
/// </summary>
public interface IRuntimePermissionAuthorizer
{
    Task<bool> IsAuthorizedAsync(
        string permissionKey,
        Guid userId,
        string role,
        CancellationToken ct = default);
}
