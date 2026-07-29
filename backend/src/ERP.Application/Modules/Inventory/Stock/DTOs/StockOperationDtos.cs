namespace ERP.Application.Modules.Inventory.Stock.DTOs;

public sealed record StockAdjustmentDto(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    string WarehouseName,
    Guid ProductId,
    string ProductName,
    decimal AdjustmentQty,
    string AdjustmentType,
    string Reason,
    string? Notes,
    DateTime AdjustmentDate,
    string Status,
    DateTime? ExecutedAt);

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
    IReadOnlyList<StockTransferLineDto> Lines);

public sealed record StockTransferLineDto(
    Guid Id,
    Guid ProductId,
    decimal Quantity,
    string Description);
