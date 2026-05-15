namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Líneas del ajuste de inventario. Tabla nueva para soportar patrón maestro-detalle.
/// </summary>
public class StockAdjustmentLine
{
    public Guid Id { get; set; }
    public Guid StockAdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal AdjustmentQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Reason { get; set; }
    public short SortOrder { get; set; }

    public StockAdjustment StockAdjustment { get; set; } = null!;
}
