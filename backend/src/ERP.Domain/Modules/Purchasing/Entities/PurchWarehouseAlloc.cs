using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchWarehouseAlloc : AuditableEntity, ISubscriberScopedEntity
{
    public Guid    PurchBillId     { get; private set; }
    public Guid    PurchBillLineId { get; private set; }
    public Guid    WarehouseId     { get; private set; }
    public Guid?   ProductId       { get; private set; }
    public decimal Quantity        { get; private set; }

    private PurchWarehouseAlloc() { }

    public static PurchWarehouseAlloc Create(
        Guid    subscriberId,
        Guid    purchBillId,
        Guid    purchBillLineId,
        Guid    warehouseId,
        Guid?   productId,
        decimal quantity,
        Guid    createdBy)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        var a = new PurchWarehouseAlloc
        {
            Id              = Guid.NewGuid(),
            SubscriberId        = subscriberId,
            PurchBillId     = purchBillId,
            PurchBillLineId = purchBillLineId,
            WarehouseId     = warehouseId,
            ProductId       = productId,
            Quantity        = quantity,
        };
        a.SetCreated(createdBy);
        return a;
    }
}
