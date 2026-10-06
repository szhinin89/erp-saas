using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Events;

public sealed class InventoryCostPostingRequestedEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid InvoiceId { get; }
    public InventoryCostPostingRequestedEvent(Guid tenantId, Guid companyId, Guid invoiceId)
    {
        TenantId = tenantId; CompanyId = companyId; InvoiceId = invoiceId;
    }
}
