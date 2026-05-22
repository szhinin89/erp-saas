namespace ERP.Application.Access.Caching;

/// <summary>
/// Única fuente de verdad para permisos runtime (post-filtro plan) de usuarios con perfil.
/// Cache-aside vía <see cref="IPermissionsCacheService"/>; la BD sigue siendo fuente de verdad.
/// Admin/read models (p. ej. <c>GetProfilePermissionsHandler</c>) no sustituyen este provider en auth.
/// </summary>
public interface IEffectivePermissionKeysProvider
{
    Task<IReadOnlyList<string>> GetAllowedKeysAsync(
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        CancellationToken ct = default);
}
