using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearCompra;

public enum ModoCreacionCompra { Xml = 1, Manual = 2 }

/// <summary>Datos de una línea de detalle en modo Manual.</summary>
public sealed record DetalleCompraInput(
    string  Descripcion,
    string? CodigoPrincipal,
    Guid?   ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal DescuentoPorcentaje,
    decimal IvaPorcentaje);

/// <summary>
/// Distribución de la cantidad de un detalle (<see cref="ItemIndex"/>) hacia una bodega.
/// <see cref="ProductoId"/> opcional: si viene, enlaza inventario aunque la línea (p. ej. XML) no tenga producto.
/// </summary>
public sealed record AsignacionBodegaRequest(int ItemIndex, Guid BodegaId, decimal Cantidad, Guid? ProductoId = null);

/// <summary>Crea una factura de compra. <c>AsignacionesBodega</c> es opcional; si viene, la suma por ítem debe igualar la cantidad de cada detalle.</summary>
[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CrearCompraCommand(
    ModoCreacionCompra Modo,

    // ── XML ──────────────────────────────────────────────────────────────
    byte[]? XmlContent,
    string? XmlNombreArchivo,

    // ── Manual ───────────────────────────────────────────────────────────
    Guid?     ProveedorId,
    string?   NumeroFactura,
    DateTime? FechaFactura,
    DateTime? FechaVencimiento,
    string?   CondicionPago,
    string?   Observaciones,
    IReadOnlyList<DetalleCompraInput>? Detalles,
    IReadOnlyList<AsignacionBodegaRequest>? AsignacionesBodega
) : IRequest<Result<CompraFacturaDto>>;
