namespace ERP.Application.Inventory.DTOs;

public record TransferDetailItemDto(
    Guid    Id,
    Guid    ProductId,
    string  Description,
    decimal Quantity);

public record TransferDto(
    Guid      Id,
    string    NumeroTransferencia,
    Guid      BodegaOrigenId,
    string    BodegaOrigenNombre,
    Guid      BodegaDestinoId,
    string    BodegaDestinoNombre,
    DateTime  FechaTransferencia,
    string    Status,
    string? Reason,
    string? Notes,
    DateTime? FechaConfirmacion,
    Guid?     ConfirmadoPor,
    DateTime  CreatedAt);

public record TransferDetailDto(
    Guid      Id,
    string    NumeroTransferencia,
    Guid      BodegaOrigenId,
    string    BodegaOrigenNombre,
    Guid      BodegaDestinoId,
    string    BodegaDestinoNombre,
    DateTime  FechaTransferencia,
    string    Status,
    string? Reason,
    string? Notes,
    DateTime? FechaConfirmacion,
    Guid?     ConfirmadoPor,
    DateTime  CreatedAt,
    IReadOnlyList<TransferDetailItemDto> Detalles);

public record TransfersPagedResult(
    IReadOnlyList<TransferDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);



