using ERP.Domain.Common;

namespace ERP.Domain.Compras.Entities;

/// <summary>
/// Línea de detalle de una <see cref="CompraFactura"/>.
/// </summary>
public sealed class CompraDetalle : AuditableEntity, ITenantEntity
{
    public Guid    CompraFacturaId      { get; private set; }
    public Guid    ProductoId           { get; private set; }
    public decimal Cantidad             { get; private set; }
    public decimal PrecioUnitario       { get; private set; }
    public decimal DescuentoPorcentaje  { get; private set; }
    public decimal Subtotal             { get; private set; }
    public decimal IvaPorcentaje        { get; private set; }
    public decimal IvaValor             { get; private set; }
    public decimal Total                { get; private set; }

    private CompraDetalle() { }

    internal static CompraDetalle Create(
        Guid    tenantId,
        Guid    compraFacturaId,
        Guid    productoId,
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
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            CompraFacturaId     = compraFacturaId,
            ProductoId          = productoId,
            Cantidad            = cantidad,
            PrecioUnitario      = precioUnitario,
            DescuentoPorcentaje = descuentoPorcentaje,
            Subtotal            = subtotal,
            IvaPorcentaje       = ivaPorcentaje,
            IvaValor            = ivaValor,
            Total               = subtotal + ivaValor,
        };
        d.SetCreated(createdBy);
        return d;
    }
}
