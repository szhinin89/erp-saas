using ERP.Domain.Common;

namespace ERP.Domain.Modules.Cash.Entities;

public sealed class PettyCashExpense : AuditableEntity, ICompanyOperationalEntity
{
    public const int DescriptionMaxLen   = 500;
    public const int VoucherTypeMaxLen   = 40;
    public const int VoucherNumberMaxLen = 80;

    public Guid?    CompanyId      { get; private set; }
    public Guid     PettyCashId    { get; private set; }
    public DateTime ExpenseDate    { get; private set; }
    public string   Description    { get; private set; } = null!;
    public decimal  Amount         { get; private set; }
    public string   VoucherType    { get; private set; } = null!;
    public string   VoucherNumber  { get; private set; } = "";
    public Guid?    JournalEntryId { get; private set; }

    private PettyCashExpense() { }

    public static PettyCashExpense Create(
        Guid     subscriberId,
        Guid     pettyCashId,
        DateTime expenseDate,
        string   description,
        decimal  amount,
        string   voucherType,
        string?  voucherNumber,
        Guid     createdBy,
        Guid?    companyId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(voucherType))
            throw new ArgumentException("Voucher type is required.", nameof(voucherType));

        var g = new PettyCashExpense
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            CompanyId      = companyId,
            PettyCashId    = pettyCashId,
            ExpenseDate    = expenseDate,
            Description    = description.Trim(),
            Amount         = amount,
            VoucherType    = voucherType.Trim(),
            VoucherNumber  = voucherNumber?.Trim() ?? "",
            JournalEntryId = null,
        };
        g.SetCreated(createdBy);
        return g;
    }

    public void LinkJournalEntry(Guid journalEntryId, Guid updatedBy)
    {
        JournalEntryId = journalEntryId;
        SetUpdated(updatedBy);
    }
}
