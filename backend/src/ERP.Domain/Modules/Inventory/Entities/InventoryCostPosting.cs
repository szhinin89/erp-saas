using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Events;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class InventoryCostPosting : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int AmountScale = 2; // Existing journal_entry_lines debit/credit numeric(18,2).
    public Guid CompanyId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string FactType { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Status { get; private set; } = "Pending";
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public int Attempts { get; private set; }
    private InventoryCostPosting() { }

    public static InventoryCostPosting Create(Guid tenantId, Guid companyId, Guid invoiceId,
        Guid sourceEventId, decimal amount, DateOnly date, string kind, Guid actorId, bool notify = false)
    {
        var row = new InventoryCostPosting
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId, InvoiceId = invoiceId,
            SourceEventId = sourceEventId, FactType = amount < 0m ? "CostOfGoodsSoldReversed" : "CostOfGoodsSold",
            Amount = Math.Round(Math.Abs(amount), AmountScale, MidpointRounding.AwayFromZero), EntryDate = date, Kind = kind
        };
        row.SetCreated(actorId);
        if (notify) row.RaiseDomainEvent(new InventoryCostPostingRequestedEvent(tenantId, companyId, invoiceId));
        return row;
    }

    public void Posted(Guid? entryId) { Status = "Posted"; JournalEntryId = entryId; Attempts++; ErrorCode = null; ErrorMessage = null; }
    public void Failed(string? code, string? message)
    {
        Status = "Failed"; Attempts++;
        ErrorCode = code is { Length: > 100 } ? code[..100] : code;
        ErrorMessage = message is { Length: > 2000 } ? message[..2000] : message;
    }
    public void Cancel() { Status = "Canceled"; }
}
