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

    public const string StockView = "inventory.stock.view";
    public const string StockManage = "inventory.stock.manage";

    public const string AdjustmentsView = "inventory.adjustments.view";
    public const string AdjustmentsCreate = "inventory.adjustments.create";
    public const string AdjustmentsUpdate = "inventory.adjustments.update";
    public const string AdjustmentsConfirm = "inventory.adjustments.confirm";
    public const string AdjustmentsCancel = "inventory.adjustments.cancel";

    public const string AdjustmentReasonsView = "inventory.adjustment-reasons.view";
    public const string AdjustmentReasonsManage = "inventory.adjustment-reasons.manage";
}
