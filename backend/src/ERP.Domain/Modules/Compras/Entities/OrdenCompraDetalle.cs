using ERP.Domain.Common;

namespace ERP.Domain.Modules.Compras.Entities;

public sealed class OrdenCompraDetalle : AuditableEntity, ITenantEntity
{
    public const int DescripcionMaxLen = 300;

    public Guid     OrdenCompraId     { get; private set; }
    public Guid     ProductoId        { get; private set; }
    public string   Descripcion       { get; private set; } = null!;
    public decimal  CantidadPedida    { get; private set; }
    public decimal  CantidadFacturada { get; private set; }
    public decimal  PrecioUnitario    { get; private set; }
    public decimal  Subtotal          { get; private set; }
    public decimal  Impuesto          { get; private set; }
    public decimal  Total             { get; private set; }

    /// <summary>Cuánto falta por facturar.</summary>
    public decimal PendienteFacturar => CantidadPedida - CantidadFacturada;

    private OrdenCompraDetalle() { }

    public static OrdenCompraDetalle Create(
        Guid    tenantId,
        Guid    ordenCompraId,
        Guid    productoId,
        string  descripcion,
        decimal cantidadPedida,
        decimal precioUnitario,
        decimal ivaPorcentaje,
        Guid    createdBy)
    {
        if (cantidadPedida <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(cantidadPedida));
        if (precioUnitario < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));

        var subtotal  = cantidadPedida * precioUnitario;
        var impuesto  = subtotal * (ivaPorcentaje / 100m);

        var d = new OrdenCompraDetalle
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            OrdenCompraId    = ordenCompraId,
            ProductoId       = productoId,
            Descripcion      = descripcion.Trim(),
            CantidadPedida   = cantidadPedida,
            CantidadFacturada = 0,
            PrecioUnitario   = precioUnitario,
            Subtotal         = subtotal,
            Impuesto         = impuesto,
            Total            = subtotal + impuesto,
        };
        d.SetCreated(createdBy);
        return d;
    }

    /// <summary>
    /// Acumula la cantidad vinculada desde una factura de compra.
    /// El caller debe validar que no se exceda CantidadPedida antes de llamar.
    /// </summary>
    public void AgregarCantidadFacturada(decimal cantidad, Guid updatedBy)
    {
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad a agregar debe ser mayor a cero.", nameof(cantidad));
        CantidadFacturada += cantidad;
        SetUpdated(updatedBy);
    }
}
