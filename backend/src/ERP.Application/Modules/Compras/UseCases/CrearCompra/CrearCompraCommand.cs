using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.CrearCompra;

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
    IReadOnlyList<DetalleCompraInput>? Detalles
) : IRequest<Result<CompraFacturaDto>>;
