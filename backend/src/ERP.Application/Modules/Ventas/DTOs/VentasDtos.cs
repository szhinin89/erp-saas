namespace ERP.Application.Ventas.DTOs;

public record VentasDetalleDto(
    Guid    Id,
    Guid    ProductoId,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    decimal Impuesto,
    decimal Total);

public record VentasFacturaDto(
    Guid      Id,
    Guid      ClienteId,
    string    ClienteNombre,
    Guid      BodegaId,
    Guid      SucursalId,
    string    Establecimiento,
    string    PuntoEmision,
    string    Secuencial,
    string    ClaveAcceso,
    DateTime  FechaEmision,
    decimal   Subtotal,
    decimal   Impuesto,
    decimal   Total,
    string    Estado,
    string?   NumeroAutorizacion,
    DateTime? FechaAutorizacion,
    string?   MensajeError,
    Guid?     AsientoContableId,
    DateTime  CreatedAt);

public record VentasFacturaDetailDto(
    Guid      Id,
    Guid      ClienteId,
    string    ClienteNombre,
    Guid      BodegaId,
    Guid      SucursalId,
    string    TipoDocumento,
    string    Establecimiento,
    string    PuntoEmision,
    string    Secuencial,
    string    ClaveAcceso,
    DateTime  FechaEmision,
    decimal   Subtotal,
    decimal   Impuesto,
    decimal   Total,
    string    Estado,
    string?   NumeroAutorizacion,
    DateTime? FechaAutorizacion,
    string?   XmlGeneradoPath,
    string?   XmlAutorizacionPath,
    string?   MensajeError,
    Guid?     AsientoContableId,
    DateTime  CreatedAt,
    IReadOnlyList<VentasDetalleDto> Detalles);

public record VentasPagedResult(
    IReadOnlyList<VentasFacturaDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record StockDisponibleDto(
    Guid    ProductoId,
    Guid    BodegaId,
    decimal CantidadDisponible,
    decimal CantidadTotal,
    decimal CantidadReservada);
