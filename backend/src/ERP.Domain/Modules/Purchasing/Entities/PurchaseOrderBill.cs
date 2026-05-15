using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseOrderBill : AuditableEntity, ITenantEntity
{
    public Guid     PurchaseOrderId { get; private set; }
    public Guid     PurchBillId     { get; private set; }
    public DateTime LinkedAt        { get; private set; }
    public Guid     LinkedBy        { get; private set; }

    private PurchaseOrderBill() { }

    public static PurchaseOrderBill Create(
        Guid tenantId,
        Guid purchaseOrderId,
        Guid purchBillId,
        Guid linkedBy)
    {
        var v = new PurchaseOrderBill
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            PurchaseOrderId = purchaseOrderId,
            PurchBillId     = purchBillId,
            LinkedAt        = DateTime.UtcNow,
            LinkedBy        = linkedBy,
        };
        v.SetCreated(linkedBy);
        return v;
    }
}
