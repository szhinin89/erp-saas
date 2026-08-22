namespace ERP.Application.Modules.Inventory.Stock.DTOs;

public sealed record StockAdjustmentDto(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    string WarehouseName,
    string MovementType,
    Guid ReasonId,
    string? ReasonName,
    string? Notes,
    DateTime AdjustmentDate,
    string Status,
    DateTime? ExecutedAt,
    Guid? ExecutedBy,
    DateTime? CancelledAt,
    Guid? CancelledBy,
    string? CancelledReason,
    IReadOnlyList<StockAdjustmentLineDto> Lines
);

public sealed record StockAdjustmentLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor,
    decimal Quantity,
    decimal QuantityInBaseUom,
    decimal? UnitCostBase,
    decimal? TotalCost,
    decimal? CurrentStockBefore,
    decimal? CurrentStockAfter,
    string? LineNotes
);

public sealed record InventoryAdjustmentReasonDto(
    Guid Id,
    string Code,
    string Name,
    string AllowedMovementType,
    bool RequiresNotes,
    bool IsActive,
    int SortOrder
);

public sealed record StockTransferDto(
    Guid Id,
    string TransferNumber,
    Guid SourceWarehouseId,
    Guid TargetWarehouseId,
    DateTime TransferDate,
    string Status,
    string? Reason,
    string? Notes,
    DateTime? ConfirmedAt,
    IReadOnlyList<StockTransferLineDto> Lines
);

public sealed record StockTransferLineDto(
    Guid Id,
    Guid ProductId,
    decimal Quantity,
    string Description
);
