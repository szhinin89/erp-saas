namespace ERP.Application.Inventario.DTOs;

public record TransferenciaDetalleDto(
    Guid    Id,
    Guid    ProductoId,
    string  Descripcion,
    decimal Cantidad);

public record TransferenciaDto(
    Guid      Id,
    string    NumeroTransferencia,
    Guid      BodegaOrigenId,
    string    BodegaOrigenNombre,
    Guid      BodegaDestinoId,
    string    BodegaDestinoNombre,
    DateTime  FechaTransferencia,
    string    Estado,
    string?   Motivo,
    string?   Observaciones,
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
    string    Estado,
    string?   Motivo,
    string?   Observaciones,
    DateTime? FechaConfirmacion,
    Guid?     ConfirmadoPor,
    DateTime  CreatedAt,
    IReadOnlyList<TransferenciaDetalleDto> Detalles);

public record TransferenciasPagedResult(
    IReadOnlyList<TransferenciaDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
