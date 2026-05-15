namespace ERP.Application.Modules.Purchasing.DTOs;

public record OrdenCompraDetalleDto(
    Guid    Id,
    Guid    ProductoId,
    string  Descripcion,
    decimal CantidadPedida,
    decimal CantidadFacturada,
    decimal PendienteFacturar,
    decimal PrecioUnitario,
    decimal Subtotal,
    decimal Impuesto,
    decimal Total);

public record OrdenCompraFacturaVinculadaDto(
    Guid     CompraFacturaId,
    string   NumeroFactura,
    DateTime FechaVinculacion);

public record OrdenCompraDto(
    Guid      Id,
    string    NumeroOrden,
    Guid      ProveedorId,
    string    ProveedorNombre,
    DateTime  FechaEmision,
    DateTime  FechaRequerida,
    string    Estado,
    string    Moneda,
    decimal   Subtotal,
    decimal   Impuesto,
    decimal   Total,
    string?   Observaciones,
    string?   DireccionEntrega,
    Guid?     BodegaDestinoId,
    DateTime? FechaEnvio,
    DateTime? FechaAprobacion,
    Guid?     AprobadoPor,
    DateTime? FechaCierre,
    DateTime  CreatedAt)
{
    /// <summary>
    /// Advertencias generadas al vincular facturas (ej. discrepancias de precio vs OC).
    /// Null si no hay advertencias o la operación no genera advertencias.
    /// </summary>
    public IReadOnlyList<string>? Advertencias { get; init; }
};

public record OrdenCompraDetailDto(
    Guid      Id,
    string    NumeroOrden,
    Guid      ProveedorId,
    string    ProveedorNombre,
    DateTime  FechaEmision,
    DateTime  FechaRequerida,
    string    Estado,
    string    Moneda,
    decimal   Subtotal,
    decimal   Impuesto,
    decimal   Total,
    string?   Observaciones,
    string?   DireccionEntrega,
    Guid?     BodegaDestinoId,
    DateTime? FechaEnvio,
    DateTime? FechaAprobacion,
    Guid?     AprobadoPor,
    DateTime? FechaCierre,
    DateTime  CreatedAt,
    IReadOnlyList<OrdenCompraDetalleDto>          Detalles,
    IReadOnlyList<OrdenCompraFacturaVinculadaDto>  FacturasVinculadas);

public record OrdenesCompraPagedResult(
    IReadOnlyList<OrdenCompraDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record ItemOrdenCompraRequest(
    Guid    ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal IvaPorcentaje = 15m);
