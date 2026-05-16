namespace ERP.Application.Inventory.DTOs;

public record StockAdjustmentDto(
    Guid      Id,
    string    AdjustmentNumber,
    Guid    WarehouseId,
    string    WarehouseName,
    Guid    ProductId,
    string    ProductName,
    decimal   AdjustmentQuantity,
    string    AdjustmentType,
    string  Reason,
    string? Notes,
    DateTime  AdjustmentDate,
    string    Status,
    DateTime? ExecutionDate,
    Guid?     ExecutedBy,
    DateTime  CreatedAt);

public record StockAdjustmentsPagedResult(
    IReadOnlyList<StockAdjustmentDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
