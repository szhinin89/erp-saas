namespace ERP.Application.Inventory.DTOs;

public record TransferDetailItemDto(
    Guid    Id,
    Guid    ProductId,
    string  Description,
    decimal Quantity);

public record TransferDto(
    Guid      Id,
    string    TransferNumber,
    Guid      SourceWarehouseId,
    string    SourceWarehouseName,
    Guid      DestinationWarehouseId,
    string    DestinationWarehouseName,
    DateTime  TransferDate,
    string    Status,
    string? Reason,
    string? Notes,
    DateTime? ConfirmationDate,
    Guid?     ConfirmedBy,
    DateTime  CreatedAt);

public record TransferDetailDto(
    Guid      Id,
    string    TransferNumber,
    Guid      SourceWarehouseId,
    string    SourceWarehouseName,
    Guid      DestinationWarehouseId,
    string    DestinationWarehouseName,
    DateTime  TransferDate,
    string    Status,
    string? Reason,
    string? Notes,
    DateTime? ConfirmationDate,
    Guid?     ConfirmedBy,
    DateTime  CreatedAt,
    IReadOnlyList<TransferDetailItemDto> Lines);

public record TransfersPagedResult(
    IReadOnlyList<TransferDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);



