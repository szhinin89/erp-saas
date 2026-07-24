namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo Inventory (items + warehouses). Catálogo de dominio —
/// referenciado por <c>[Authorize(Policy = $"perm:{...}")]</c> en ERP.API y por
/// <see cref="Modules.InventoryModule"/> para la navegación derivada.
/// </summary>
public static class InventoryPermissions
{
    public const string ItemsView = "items.view";
    public const string ItemsCreate = "items.create";
    public const string ItemsEdit = "items.edit";

    public const string WarehousesView = "inventory.warehouses.view";
    public const string WarehousesCreate = "inventory.warehouses.create";
    public const string WarehousesUpdate = "inventory.warehouses.update";
    public const string WarehousesDelete = "inventory.warehouses.delete";

    public const string StockView   = "inventory.stock.view";
    public const string StockManage = "inventory.stock.manage";
}
