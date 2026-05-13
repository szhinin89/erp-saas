using ERP.Domain.Common;

namespace ERP.Domain.Modules.Compras.Entities;

/// <summary>
/// Línea de detalle de una <see cref="CompraFactura"/>.
/// ProductoId es nullable: en compras por XML el ítem puede no tener
/// correspondencia en el catálogo de productos del tenant.
/// </summary>
public sealed class CompraDetalle : AuditableEntity, ITenantEntity
{
    public const int DescripcionMaxLen           = 300;
    public const int CodigoPrincipalMaxLen       = 50;

    public Guid    CompraFacturaId            { get; private set; }
    public Guid?   ProductoId                 { get; private set; }   // nullable: puede no existir
    public Guid?   OrdenCompraDetalleId       { get; private set; }   // nullable: la línea puede no estar vinculada a OC
    public string  Descripcion               { get; private set; } = null!;
    public string? CodigoPrincipalProveedor  { get; private set; }   // código del emisor (XML)
    public decimal Cantidad                  { get; private set; }
    public decimal PrecioUnitario            { get; private set; }
    public decimal DescuentoPorcentaje       { get; private set; }
    public decimal Subtotal                  { get; private set; }
    public decimal IvaPorcentaje             { get; private set; }
    public decimal IvaValor                  { get; private set; }
    public decimal Total                     { get; private set; }

    private CompraDetalle() { }

    /// <summary>Vincula esta línea a la línea de una OC para trazabilidad.</summary>
    public void SetOrdenCompraDetalleId(Guid ordenCompraDetalleId, Guid updatedBy)
    {
        OrdenCompraDetalleId = ordenCompraDetalleId;
        SetUpdated(updatedBy);
    }

    internal static CompraDetalle Create(
        Guid    tenantId,
        Guid    compraFacturaId,
        string  descripcion,
        string? codigoPrincipalProveedor,
        Guid?   productoId,
        decimal cantidad,
        decimal precioUnitario,
        decimal descuentoPorcentaje,
        decimal ivaPorcentaje,
        Guid    createdBy)
    {
        var subtotal = cantidad * precioUnitario * (1 - descuentoPorcentaje / 100m);
        var ivaValor = subtotal * (ivaPorcentaje / 100m);

        var d = new CompraDetalle
        {
            Id                       = Guid.NewGuid(),
            TenantId                 = tenantId,
            CompraFacturaId          = compraFacturaId,
            ProductoId               = productoId,
            Descripcion              = descripcion.Trim(),
            CodigoPrincipalProveedor = string.IsNullOrWhiteSpace(codigoPrincipalProveedor)
                                        ? null : codigoPrincipalProveedor.Trim(),
            Cantidad                 = cantidad,
            PrecioUnitario           = precioUnitario,
            DescuentoPorcentaje      = descuentoPorcentaje,
            Subtotal                 = subtotal,
            IvaPorcentaje            = ivaPorcentaje,
            IvaValor                 = ivaValor,
            Total                    = subtotal + ivaValor,
        };
        d.SetCreated(createdBy);
        return d;
    }
}
