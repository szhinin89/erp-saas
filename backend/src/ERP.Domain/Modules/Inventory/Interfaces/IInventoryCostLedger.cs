using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IInventoryCostLedger
{
    Task<IReadOnlyList<InventoryCostPosting>> GetInvoicePostingsAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<IReadOnlyList<InventoryCostPosting>> GetPendingPostingsAsync(Guid tenantId, Guid companyId, CancellationToken ct);
    Task<IReadOnlyList<SaleCostObligation>> GetPendingObligationsAsync(Guid tenantId, Guid companyId, CancellationToken ct);
    Task LockInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<decimal?> GetCurrentInvoiceCostAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    void Add(InventoryCostPosting posting);
    Task<int> SaveAsync(CancellationToken ct);
}
