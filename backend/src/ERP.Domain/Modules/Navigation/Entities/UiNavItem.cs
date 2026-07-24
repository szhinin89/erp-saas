namespace ERP.Domain.Navigation.Entities;

/// <summary>
/// Ítem de menú bajo un <see cref="UiNavGroup"/> (ruta React + reglas de visibilidad).
/// </summary>
public sealed class UiNavItem
{
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    /// <summary>Ítem padre dentro del mismo grupo; null = raíz del grupo.</summary>
    public Guid? ParentItemId { get; private set; }
    public string RoutePath { get; private set; } = null!;
    public string LabelKey { get; private set; } = null!;
    /// <summary>Texto mostrado si no se usa solo i18n (<c>LabelKey</c>). Opcional; p. ej. ítems creados desde panel platform.</summary>
    public string? DisplayLabel { get; private set; }
    public int SortOrder { get; private set; }
    public string? ModuleKey { get; private set; }
    public string? PermissionKey { get; private set; }
    /// <summary>JSON array de claves de permiso (OR); null si no aplica.</summary>
    public string? PermissionKeysAnyJson { get; private set; }
    public string? RolesCsv { get; private set; }
    public bool IsActive { get; private set; }
    private UiNavItem() { }

    public static UiNavItem Create(
        Guid id,
        Guid groupId,
        string routePath,
        string labelKey,
        int sortOrder,
        string? moduleKey,
        string? permissionKey,
        string? permissionKeysAnyJson,
        string? rolesCsv,
        bool isActive = true,
        Guid? parentItemId = null,
        string? displayLabel = null)
    {
        return new UiNavItem
        {
            Id = id,
            GroupId = groupId,
            ParentItemId = parentItemId,
            RoutePath = (routePath ?? string.Empty).Trim(),
            LabelKey = (labelKey ?? string.Empty).Trim(),
            DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim(),
            SortOrder = sortOrder,
            ModuleKey = string.IsNullOrWhiteSpace(moduleKey) ? null : moduleKey.Trim().ToLowerInvariant(),
            PermissionKey = string.IsNullOrWhiteSpace(permissionKey) ? null : permissionKey.Trim(),
            PermissionKeysAnyJson = string.IsNullOrWhiteSpace(permissionKeysAnyJson) ? null : permissionKeysAnyJson.Trim(),
            RolesCsv = string.IsNullOrWhiteSpace(rolesCsv) ? null : rolesCsv.Trim(),
            IsActive = isActive,
        };
    }

    public void SyncFrom(
        Guid groupId,
        string routePath,
        string labelKey,
        int sortOrder,
        string? moduleKey,
        string? permissionKey,
        string? permissionKeysAnyJson,
        Guid? parentItemId)
    {
        GroupId = groupId;
        ParentItemId = parentItemId;
        RoutePath = (routePath ?? string.Empty).Trim();
        LabelKey = (labelKey ?? string.Empty).Trim();
        SortOrder = sortOrder;
        ModuleKey = string.IsNullOrWhiteSpace(moduleKey) ? null : moduleKey.Trim().ToLowerInvariant();
        PermissionKey = string.IsNullOrWhiteSpace(permissionKey) ? null : permissionKey.Trim();
        PermissionKeysAnyJson = string.IsNullOrWhiteSpace(permissionKeysAnyJson) ? null : permissionKeysAnyJson.Trim();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}
