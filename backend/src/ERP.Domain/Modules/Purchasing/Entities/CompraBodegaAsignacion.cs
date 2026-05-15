using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

/// <summary>
/// Asignación de una línea de compra a una bodega específica.
/// Permite distribuir la cantidad recibida entre varias bodegas.
/// </summary>
public sealed class CompraBodegaAsignacion : AuditableEntity, ITenantEntity
{
    public Guid    CompraFacturaId  { get; private set; }
    public Guid    CompraDetalleId  { get; private set; }
    public Guid    BodegaId         { get; private set; }
    /// <summary>Null si la línea de compra no está enlazada a un producto del catálogo (no aplica movimiento de inventario).</summary>
    public Guid?   ProductoId       { get; private set; }
    public decimal Cantidad         { get; private set; }

    private CompraBodegaAsignacion() { }

    public static CompraBodegaAsignacion Create(
        Guid    tenantId,
        Guid    compraFacturaId,
        Guid    compraDetalleId,
        Guid    bodegaId,
        Guid?   productoId,
        decimal cantidad,
        Guid    createdBy)
    {
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(cantidad));

        var a = new CompraBodegaAsignacion
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            CompraFacturaId = compraFacturaId,
            CompraDetalleId = compraDetalleId,
            BodegaId        = bodegaId,
            ProductoId      = productoId,
            Cantidad        = cantidad,
        };
        a.SetCreated(createdBy);
        return a;
    }
}
