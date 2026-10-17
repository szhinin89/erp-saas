using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Registro legacy de contabilización de costo de ventas (tabla <c>inventory_cost_postings</c>).
/// ACCOUNTING-CONTINUITY-05: solo lectura histórica — sus filas se migraron a
/// <c>AccountingPosting</c>, autoridad contable única; ya no recibe escrituras.
/// </summary>
public sealed class InventoryCostPosting : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string FactType { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public int Attempts { get; private set; }
    private InventoryCostPosting() { }
}
