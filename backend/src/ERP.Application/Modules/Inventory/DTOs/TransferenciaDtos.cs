namespace ERP.Application.Inventory.DTOs;

public record TransferenciaDetalleDto(
    Guid    Id,
    Guid    ProductId,
    string  Description,
    decimal Quantity);

public record TransferenciaDto(
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

public record TransferenciaDetailDto(
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
    IReadOnlyList<TransferenciaDetalleDto> Detalles);

public record TransferenciasPagedResult(
    IReadOnlyList<TransferenciaDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
