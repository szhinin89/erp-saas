namespace ERP.Application.Modules.Inventory.Stock.DTOs;

public sealed record CurrentStockDto(
    Guid Id,
    Guid ItemId,
    Guid WarehouseId,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal TotalStockValue,
    decimal AverageCost,
    DateTime LastUpdatedAt
);

public sealed record StockMovementDto(
    Guid Id,
    Guid ItemId,
    Guid WarehouseId,
    int MovementType,
    string MovementTypeName,
    decimal Quantity,
    string UomCode,
    decimal PreviousQuantity,
    decimal ResultQuantity,
    long SequenceNumber,
    decimal? UnitCost,
    decimal? TotalCost,
    decimal RunningAverageCost,
    decimal RunningStockValue,
    DateOnly EffectiveDate,
    string? Reference,
    Guid? SourceDocId,
    string? SourceDocType,
    Guid CreatedBy,
    DateTime CreatedAt,
    string? CreatedByName = null
);

public sealed record AggregatedStockDto(
    Guid ItemId,
    decimal TotalQuantity,
    decimal TotalStockValue,
    decimal AverageCost
);

public sealed record ItemWarehouseAvailabilityDto(
    Guid WarehouseId,
    string WarehouseName,
    decimal Available,
    decimal Reserved,
    bool CanSell
);
