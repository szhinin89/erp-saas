using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>Lectura y reordenamiento del menú principal (tablas <c>ui_nav_*</c>) para SuperAdmin.</summary>
public interface INavigationMenuAdminService
{
    Task<AdminNavigationMenuResponse> GetMenuTreeAsync(CancellationToken ct = default);

    /// <summary>Actualiza <c>sort_order</c> de grupos activos según el orden del array (debe listar todos los grupos activos exactamente una vez).</summary>
    Task<(bool Ok, string? Error)> ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken ct = default);

    /// <summary>Actualiza <c>sort_order</c> de ítems por nivel (grupo + padre opcional).</summary>
    Task<(bool Ok, string? Error)> ReorderItemLevelsAsync(IReadOnlyList<NavItemSiblingOrderDto> levels, CancellationToken ct = default);
}
