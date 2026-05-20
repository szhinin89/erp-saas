using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseOrderBill : AuditableEntity, ISubscriberScopedEntity
{
    public Guid     PurchaseOrderId { get; private set; }
    public Guid     PurchBillId     { get; private set; }
    public DateTime LinkedAt        { get; private set; }
    public Guid     LinkedBy        { get; private set; }

    private PurchaseOrderBill() { }

    public static PurchaseOrderBill Create(
        Guid subscriberId,
        Guid purchaseOrderId,
        Guid purchBillId,
        Guid linkedBy)
    {
        var v = new PurchaseOrderBill
        {
            Id              = Guid.NewGuid(),
            SubscriberId        = subscriberId,
            PurchaseOrderId = purchaseOrderId,
            PurchBillId     = purchBillId,
            LinkedAt        = DateTime.UtcNow,
            LinkedBy        = linkedBy,
        };
        v.SetCreated(linkedBy);
        return v;
    }
}
