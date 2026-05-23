using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>Lectura y reordenamiento del menú principal (tablas <c>ui_nav_*</c>) para operador platform.</summary>
public interface INavigationMenuAdminService
{
    Task<AdminNavigationMenuResponse> GetMenuTreeAsync(CancellationToken ct = default);

    /// <summary>Actualiza <c>sort_order</c> de grupos activos según el orden del array (debe listar todos los grupos activos exactamente una vez).</summary>
    Task<(bool Ok, string? Error)> ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken ct = default);

    /// <summary>
    /// Aplica la jerarquía y el orden: cada ítem activo de los grupos incluidos aparece exactamente en un nivel
    /// (<c>group_id</c> + <c>parent_item_id</c>); actualiza <c>parent_item_id</c> y <c>sort_order</c>.
    /// </summary>
    Task<(bool Ok, string? Error)> ReorderItemLevelsAsync(IReadOnlyList<NavItemSiblingOrderDto> levels, CancellationToken ct = default);

    /// <summary>Crea un ítem activo; <c>label_key</c> interno <c>nav.custom.*</c> y texto visible en <c>display_label</c>.</summary>
    Task<(bool Ok, Guid? NewId, string? Error)> CreateNavItemAsync(CreateNavItemRequest request, CancellationToken ct = default);

    Task<(bool Ok, string? Error)> UpdateNavItemAsync(Guid itemId, UpdateNavItemRequest request, CancellationToken ct = default);

    /// <summary>Desactiva el ítem y todo su subárbol (no borra filas).</summary>
    Task<(bool Ok, string? Error)> DeleteNavItemAsync(Guid itemId, CancellationToken ct = default);
}
