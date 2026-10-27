namespace ERP.Application.Access.Authorization;

/// <summary>
/// Autorización runtime de permisos (<c>perm:*</c>).
/// Única autoridad de decisión en Application: delega contexto a <see cref="ICompanyContextProvider"/>
/// y claves efectivas a <see cref="Caching.IEffectivePermissionKeysProvider"/>.
/// </summary>
public interface IRuntimePermissionAuthorizer
{
    /// <param name="role">
    /// Rol del JWT. SEC-04: no es autoridad — el bypass Admin se decide por la membership de la
    /// empresa operativa o por el operador global (<see cref="OperationalCompanyContext.HasAdminBypass"/>).
    /// </param>
    Task<bool> IsAuthorizedAsync(
        string permissionKey,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default
    );
}
