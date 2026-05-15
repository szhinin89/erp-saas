using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class VentasNotaDetalle : AuditableEntity, ITenantEntity
{
    public const int DescripcionMaxLen = 300;

    public Guid VentasNotaCreditoDebitoId { get; private set; }
    public Guid ProductoId { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal Total { get; private set; }
    public string Descripcion { get; private set; } = null!;

    private VentasNotaDetalle() { }

    public static VentasNotaDetalle Create(
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

        var d = new VentasNotaDetalle
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            VentasNotaCreditoDebitoId = Guid.Empty,
            ProductoId   = productoId,
            Cantidad     = cantidad,
            PrecioUnitario = precioUnitario,
            Subtotal     = subtotal,
            Impuesto     = impuesto,
            Total        = total,
            Descripcion  = (descripcion ?? string.Empty).Trim(),
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AsignarNotaId(Guid notaId)
    {
        if (VentasNotaCreditoDebitoId != Guid.Empty)
            throw new InvalidOperationException("El detalle ya está asignado a una nota.");
        VentasNotaCreditoDebitoId = notaId;
    }
}
