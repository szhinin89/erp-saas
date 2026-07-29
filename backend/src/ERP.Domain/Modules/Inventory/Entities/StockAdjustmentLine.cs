using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Línea de ajuste de inventario. CompanyId heredado del StockAdjustment padre.
/// EF Core popula las propiedades via reflection — setters private son seguros.
/// </summary>
public sealed class StockAdjustmentLine : ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid StockAdjustmentId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public decimal SystemQuantity { get; private set; }
    public decimal PhysicalQuantity { get; private set; }
    public decimal AdjustmentQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string? Reason { get; private set; }
    public short SortOrder { get; private set; }

    public StockAdjustment StockAdjustment { get; private set; } = null!;

    private StockAdjustmentLine() { }

    public static StockAdjustmentLine Create(
        Guid stockAdjustmentId,
        Guid tenantId,
        Guid companyId,
        Guid productId,
        Guid warehouseId,
        decimal systemQuantity,
        decimal physicalQuantity,
        decimal unitCost,
        string? reason,
        short sortOrder
    )
    {
        return new StockAdjustmentLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            StockAdjustmentId = stockAdjustmentId,
            ProductId = productId,
            WarehouseId = warehouseId,
            SystemQuantity = systemQuantity,
            PhysicalQuantity = physicalQuantity,
            AdjustmentQuantity = physicalQuantity - systemQuantity,
            UnitCost = unitCost,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            SortOrder = sortOrder,
        };
    }
}
