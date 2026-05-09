namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Parsea un comprobante electrónico SRI Ecuador (XML) y extrae los datos relevantes.
/// </summary>
public interface IXmlFacturaParser
{
    /// <summary>
    /// Lee el stream XML y retorna los datos estructurados del comprobante.
    /// </summary>
    /// <exception cref="XmlParseException">Si falta algún nodo crítico o el XML es inválido.</exception>
    Task<FacturaParseResult> ParseAsync(Stream xmlStream, CancellationToken ct = default);
}

// ── Resultado del parseo ──────────────────────────────────────────────────────

/// <summary>
/// Datos extraídos de una factura electrónica SRI.
/// </summary>
/// <param name="ClaveAcceso">49 dígitos que identifican unívocamente el comprobante.</param>
/// <param name="NumeroFactura">Formato estab-ptoEmi-secuencial (p.ej. 001-001-000000001).</param>
/// <param name="FechaEmision">Fecha de emisión del comprobante (DD/MM/YYYY → DateTime).</param>
/// <param name="RucProveedor">RUC de 13 dígitos del emisor.</param>
/// <param name="RazonSocialProveedor">Razón social del emisor.</param>
/// <param name="Subtotal">Total sin impuestos.</param>
/// <param name="Impuesto">Suma de todos los impuestos (IVA + ICE + otros).</param>
/// <param name="Total">Importe total del comprobante.</param>
/// <param name="Items">Líneas de detalle del comprobante.</param>
public sealed record FacturaParseResult(
    string                    ClaveAcceso,
    string                    NumeroFactura,
    DateTime                  FechaEmision,
    string                    RucProveedor,
    string                    RazonSocialProveedor,
    decimal                   Subtotal,
    decimal                   Impuesto,
    decimal                   Total,
    IReadOnlyList<ItemFactura> Items
);

/// <summary>Línea de detalle de la factura.</summary>
/// <param name="CodigoPrincipal">Código interno del producto/servicio del emisor.</param>
/// <param name="Descripcion">Descripción del ítem.</param>
/// <param name="Cantidad">Cantidad (puede tener decimales).</param>
/// <param name="PrecioUnitario">Precio unitario sin impuestos.</param>
/// <param name="Descuento">Descuento en valor monetario.</param>
/// <param name="Subtotal">precioTotalSinImpuesto (cantidad × precio − descuento).</param>
public sealed record ItemFactura(
    string  CodigoPrincipal,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Subtotal
);
