namespace ERP.Application.Access;

/// <summary>
/// Marca queries/handlers de solo lectura administrativa.
/// NO usar en runtime auth — la decisión de acceso pasa por
/// <see cref="Authorization.IRuntimePermissionAuthorizer"/> y
/// <see cref="Caching.IEffectivePermissionKeysProvider"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AdminReadModelAttribute : Attribute
{
    public AdminReadModelAttribute(string purpose) => Purpose = purpose;

    public string Purpose { get; }
}
