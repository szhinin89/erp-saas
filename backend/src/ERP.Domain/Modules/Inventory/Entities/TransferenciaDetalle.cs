using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class TransferenciaDetalle : AuditableEntity, ITenantEntity
{
    public const int DescripcionMaxLen = 300;

    public Guid    TransferenciaId { get; private set; }
    public Guid    ProductoId      { get; private set; }
    public decimal Cantidad        { get; private set; }
    public string  Descripcion     { get; private set; } = null!;

    // English aliases for gradual migration
    public Guid StockTransferId => TransferenciaId;
    public Guid ProductId => ProductoId;
    public decimal Quantity => Cantidad;
    public string Description => Descripcion;

    private TransferenciaDetalle() { }

    public static TransferenciaDetalle Create(
        Guid    tenantId,
        Guid    transferenciaId,
        Guid    productoId,
        decimal cantidad,
        string  descripcion,
        Guid    createdBy)
    {
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(cantidad));

        var d = new TransferenciaDetalle
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            TransferenciaId = transferenciaId,
            ProductoId      = productoId,
            Cantidad        = cantidad,
            Descripcion     = descripcion.Trim(),
        };
        d.SetCreated(createdBy);
        return d;
    }
}
