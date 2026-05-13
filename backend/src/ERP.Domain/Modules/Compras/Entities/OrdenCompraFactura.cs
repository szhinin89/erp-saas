using ERP.Domain.Common;

namespace ERP.Domain.Modules.Compras.Entities;

/// <summary>
/// Vinculación entre una OrdenCompra y una CompraFactura (muchos a muchos).
/// Permite relacionar una o varias facturas electrónicas con una OC para
/// controlar lo pedido vs lo facturado.
/// </summary>
public sealed class OrdenCompraFactura : AuditableEntity, ITenantEntity
{
    public Guid     OrdenCompraId    { get; private set; }
    public Guid     CompraFacturaId  { get; private set; }
    public DateTime FechaVinculacion { get; private set; }
    public Guid     VinculadoPor     { get; private set; }

    private OrdenCompraFactura() { }

    public static OrdenCompraFactura Create(
        Guid tenantId,
        Guid ordenCompraId,
        Guid compraFacturaId,
        Guid vinculadoPor)
    {
        var v = new OrdenCompraFactura
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            OrdenCompraId   = ordenCompraId,
            CompraFacturaId = compraFacturaId,
            FechaVinculacion = DateTime.UtcNow,
            VinculadoPor    = vinculadoPor,
        };
        v.SetCreated(vinculadoPor);
        return v;
    }
}
