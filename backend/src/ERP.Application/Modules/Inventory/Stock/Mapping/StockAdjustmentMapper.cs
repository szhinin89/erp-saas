using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Application.Modules.Inventory.Stock.Mapping;

/// <summary>
/// Mapper compartido StockAdjustment → DTO. Único punto de mapeo — reutilizado por Create/Update/
/// Execute/Cancel/Get/List (INVENTORY-ADJUSTMENTS-02) para no duplicar la forma del DTO.
/// </summary>
internal static class StockAdjustmentMapper
{
    public static StockAdjustmentDto ToDto(StockAdjustment a, string? reasonName) =>
        new(
            a.Id,
            a.AdjustmentNumber,
            a.WarehouseId,
            a.WarehouseName,
            a.MovementType,
            a.ReasonId,
            reasonName,
            a.Notes,
            a.AdjustmentDate,
            a.Status,
            a.ExecutedAt,
            a.ExecutedBy,
            a.CancelledAt,
            a.CancelledBy,
            a.CancelledReason,
            a.Lines.OrderBy(l => l.SortOrder).Select(ToLineDto).ToList()
        );

    public static StockAdjustmentLineDto ToLineDto(StockAdjustmentLine l) =>
        new(
            l.Id,
            l.ItemId,
            l.ItemName,
            l.PackagingLevelId,
            l.UomCode,
            l.BaseUomCode,
            l.ConversionFactor,
            l.Quantity,
            l.QuantityInBaseUom,
            l.UnitCostBase,
            l.TotalCost,
            l.CurrentStockBefore,
            l.CurrentStockAfter,
            l.LineNotes
        );

    public static InventoryAdjustmentReasonDto ToDto(InventoryAdjustmentReason r) =>
        new(r.Id, r.Code, r.Name, r.AllowedMovementType, r.RequiresNotes, r.IsActive, r.SortOrder);
}
