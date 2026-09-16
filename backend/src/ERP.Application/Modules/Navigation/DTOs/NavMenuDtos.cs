namespace ERP.Application.Navigation.DTOs;

/// <summary>Grupo del menú lateral ya filtrado por permisos del backend — seguro para consumo directo en frontend.</summary>
/// <remarks>
/// MAPA-MENU-ERP-SSOT-01: <see cref="SortOrder"/> agregado — antes el orden real de los tiles del
/// launcher (Módulo, nivel 1) nunca llegaba al frontend pese a que <c>[Module(SortOrder=N)]</c> ya
/// existía en el backend; el frontend rellenaba el hueco con una lista fija propia
/// (<c>MAIN_NAV_GROUP_ORDER</c> en navConfig.ts), es decir, hardcodeaba el orden de módulos. Con
/// este campo, <c>sortNavGroupsForMainBar</c> usa el SortOrder real del backend cuando está
/// presente — SSOT de navegación completo, sin lista de módulos hardcodeada en frontend para los
/// grupos reales (los sintéticos como "home"/"access"/"security" siguen usando el fallback, ya que
/// no provienen de un <c>[Module]</c> real).
/// </remarks>
public sealed record NavMenuGroupDto(
    Guid Id,
    string Code,
    string Icon,
    string LabelKey,
    int SortOrder,
    IReadOnlyList<NavMenuItemDto> Items
);

/// <summary>Ítem de menú ya filtrado — sin metadatos de permiso ni rol.</summary>
public sealed record NavMenuItemDto(
    Guid Id,
    string LabelKey,
    string? DisplayLabel,
    string RoutePath,
    IReadOnlyList<NavMenuItemDto> Children
);
