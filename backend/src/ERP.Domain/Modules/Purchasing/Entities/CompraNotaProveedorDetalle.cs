using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class CompraNotaProveedorDetalle : AuditableEntity, ITenantEntity
{
    public const int DescripcionMaxLen = 300;
    public const int CodigoPrincipalMaxLen = 50;

    public Guid CompraNotaProveedorId { get; private set; }
    public Guid? ProductoId { get; private set; }
    public string? CodigoPrincipalProveedor { get; private set; }
    public string Descripcion { get; private set; } = null!;
    public decimal Cantidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal Total { get; private set; }

    private CompraNotaProveedorDetalle() { }

    public static CompraNotaProveedorDetalle Create(
        Guid tenantId,
        Guid? productoId,
        string? codigoPrincipalProveedor,
        string descripcion,
        decimal cantidad,
        decimal precioUnitario,
        decimal subtotal,
        decimal impuesto,
        decimal total,
        Guid createdBy)
    {
        var d = new CompraNotaProveedorDetalle
        {
            Id                        = Guid.NewGuid(),
            TenantId                  = tenantId,
            CompraNotaProveedorId     = Guid.Empty,
            ProductoId                = productoId,
            CodigoPrincipalProveedor  = string.IsNullOrWhiteSpace(codigoPrincipalProveedor) ? null : codigoPrincipalProveedor.Trim(),
            Descripcion               = (descripcion ?? string.Empty).Trim(),
            Cantidad                  = cantidad,
            PrecioUnitario            = precioUnitario,
            Subtotal                  = subtotal,
            Impuesto                  = impuesto,
            Total                     = total,
        };
        d.SetCreated(createdBy);
        return d;
    }

    public void AsignarNotaId(Guid notaId)
    {
        if (CompraNotaProveedorId != Guid.Empty)
            throw new InvalidOperationException("El detalle ya está asignado a una nota.");
        CompraNotaProveedorId = notaId;
    }
}
