namespace ERP.Domain.Kernel.Navigation;

/// <summary>
/// Ítem de navegación global resuelto desde <see cref="Attributes.NavItemAttribute"/>.
/// Equivale a una fila de <c>ui_nav_items</c>. <see cref="ParentItemId"/> agrupa el ítem
/// bajo otro ítem (contenedor) del mismo grupo. <see cref="PermissionKeysAnyJson"/> es un
/// JSON array de claves de permiso (OR) — ver <see cref="Attributes.NavItemAttribute.PermissionsAnyCsv"/>.
/// <see cref="RelatedActionPermissionKeys"/> — ver
/// <see cref="Attributes.NavItemAttribute.RelatedActionPermissionsCsv"/>. ADMIN-PERMISSIONS-SSOT-
/// KERNEL-02: campo solo en memoria, no se persiste en <c>ui_nav_items</c>/
/// <c>NavigationSyncService</c> — no afecta el menú, solo lo consume el catálogo de permisos
/// asignables (<see cref="KernelRegistry.AssignablePermissionKeys"/>).
/// </summary>
public sealed record NavigationItemDefinition(
    Guid Id,
    Guid GroupId,
    string GroupCode,
    string RoutePath,
    string LabelKey,
    string? PermissionKey,
    int SortOrder,
    Guid? ParentItemId = null,
    string? PermissionKeysAnyJson = null,
    IReadOnlyList<string>? RelatedActionPermissionKeys = null,
    string? FeatureKey = null,
    bool RequiresExternalEntitlement = false
);
