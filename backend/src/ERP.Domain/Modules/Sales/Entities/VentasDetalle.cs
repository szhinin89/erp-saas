using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class VentasDetalle : AuditableEntity, ITenantEntity
{
    public const int DescripcionMaxLen = 300;

    public Guid VentasFacturaId { get; private set; }
    public Guid ProductoId { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal Total { get; private set; }
    public string Descripcion { get; private set; } = null!;

    private VentasDetalle() { }

    public static VentasDetalle Create(
        Guid tenantId,
        Guid productoId,
        decimal cantidad,
        decimal precioUnitario,
        decimal impuesto,
        string descripcion,
        Guid createdBy)
    {
        var subtotal = cantidad * precioUnitario;
        var total = subtotal + impuesto;

        var detalle = new VentasDetalle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VentasFacturaId = Guid.Empty, // Se asignará después
            ProductoId = productoId,
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            Subtotal = subtotal,
            Impuesto = impuesto,
            Total = total,
            Descripcion = descripcion.Trim(),
        };
        detalle.SetCreated(createdBy);
        return detalle;
    }

    public void AsignarFacturaId(Guid facturaId)
    {
        if (VentasFacturaId != Guid.Empty)
            throw new InvalidOperationException("El detalle ya está asignado a una factura.");
        VentasFacturaId = facturaId;
    }
}
