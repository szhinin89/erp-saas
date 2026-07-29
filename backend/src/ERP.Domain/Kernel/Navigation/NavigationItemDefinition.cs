namespace ERP.Domain.Kernel.Navigation;

/// <summary>
/// Ítem de navegación global resuelto desde <see cref="Attributes.NavItemAttribute"/>.
/// Equivale a una fila de <c>ui_nav_items</c>. <see cref="ParentItemId"/> agrupa el ítem
/// bajo otro ítem (contenedor) del mismo grupo. <see cref="PermissionKeysAnyJson"/> es un
/// JSON array de claves de permiso (OR) — ver <see cref="Attributes.NavItemAttribute.PermissionsAnyCsv"/>.
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
    string? PermissionKeysAnyJson = null
);
