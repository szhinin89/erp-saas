using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Líneas del ajuste. Comparte company scope con <see cref="StockAdjustment"/>.
/// </summary>
public class StockAdjustmentLine : ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid Id { get; set; }
    public Guid SubscriberId { get; set; }
    public Guid? CompanyId { get; set; }
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
