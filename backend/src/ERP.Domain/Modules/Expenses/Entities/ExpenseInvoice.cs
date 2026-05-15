using ERP.Domain.Common;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Domain.Modules.Expenses.Entities;

public sealed class ExpenseInvoice : MasterEntity, ITenantEntity
{
    public const int AccessKeyLen         = 49;
    public const int XmlPathMaxLen        = 500;
    public const int InvoiceNumberMaxLen  = 50;
    public const int ConceptMaxLen        = 200;
    public const int CategoryMaxLen       = 80;
    public const int NotesMaxLen          = 1000;
    public const int RejectionReasonMaxLen = 500;

    public const decimal RequiresXmlThreshold = 100m;
    public const decimal TotalTolerance       = 0.01m;

    public string?       AccessKey          { get; private set; }
    public DateTime      IssueDate          { get; private set; }
    public Guid?         SupplierId         { get; private set; }
    public string?       InvoiceNumber      { get; private set; }
    public string        Concept            { get; private set; } = null!;
    public string        Category           { get; private set; } = null!;
    public decimal       Subtotal           { get; private set; }
    public decimal       TaxTotal           { get; private set; }
    public decimal       Total              { get; private set; }
    public ExpenseStatus Status             { get; private set; } = ExpenseStatus.Draft;
    public string?       XmlPath            { get; private set; }
    public string?       Notes              { get; private set; }
    public Guid?         ValidatedBy        { get; private set; }
    public DateTime?     ValidatedAt        { get; private set; }
    public Guid?         ApprovedBy         { get; private set; }
    public DateTime?     ApprovedAt         { get; private set; }
    public Guid?         RejectedBy         { get; private set; }
    public DateTime?     RejectedAt         { get; private set; }
    public string?       RejectionReason    { get; private set; }
    public Guid?         JournalEntryId     { get; private set; }
    public decimal       TotalNotesApplied  { get; private set; }

    public ICollection<ExpenseDetail> Details { get; set; } = [];

    private ExpenseInvoice() { }

    public static ExpenseInvoice CreateManual(
        Guid     tenantId,
        Guid?    supplierId,
        DateTime issueDate,
        string   concept,
        string   category,
        decimal  subtotal,
        decimal  taxTotal,
        decimal  total,
        string?  notes,
        Guid     createdBy)
    {
        ValidateAmounts(subtotal, taxTotal, total);
        if (total > RequiresXmlThreshold)
            throw new InvalidOperationException(
                $"Expenses over {RequiresXmlThreshold} require an XML voucher.");

        var g = new ExpenseInvoice
        {
            Id         = Guid.NewGuid(),
            TenantId   = tenantId,
            SupplierId = supplierId,
            IssueDate  = issueDate,
            Concept    = concept.Trim(),
            Category   = category.Trim(),
            Subtotal   = subtotal,
            TaxTotal   = taxTotal,
            Total      = total,
            Status     = ExpenseStatus.Draft,
            Notes      = Trim(notes),
        };
        g.SetCreated(createdBy);
        return g;
    }

    public static ExpenseInvoice CreateFromXml(
        Guid     tenantId,
        Guid     supplierId,
        string   accessKey,
        string?  invoiceNumber,
        DateTime issueDate,
        string   concept,
        string   category,
        decimal  subtotal,
        decimal  taxTotal,
        decimal  total,
        string   xmlPath,
        string?  notes,
        Guid     createdBy)
    {
        ValidateAccessKey(accessKey);
        ValidateAmounts(subtotal, taxTotal, total);

        var g = new ExpenseInvoice
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            SupplierId    = supplierId,
            AccessKey     = accessKey.Trim(),
            InvoiceNumber = Trim(invoiceNumber),
            IssueDate     = issueDate,
            Concept       = concept.Trim(),
            Category      = category.Trim(),
            Subtotal      = subtotal,
            TaxTotal      = taxTotal,
            Total         = total,
            XmlPath       = xmlPath.Trim(),
            Status        = ExpenseStatus.Draft,
            Notes         = Trim(notes),
        };
        g.SetCreated(createdBy);
        return g;
    }

    public void Validate(Guid userId)
    {
        if (Status != ExpenseStatus.Draft)
            throw new InvalidOperationException("Only Draft expenses can be validated.");
        Status      = ExpenseStatus.Validated;
        ValidatedBy = userId;
        ValidatedAt = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Approve(Guid userId, Guid journalEntryId)
    {
        if (Status != ExpenseStatus.Validated)
            throw new InvalidOperationException("Only Validated expenses can be approved.");
        Status         = ExpenseStatus.Approved;
        ApprovedBy     = userId;
        ApprovedAt     = DateTime.UtcNow;
        JournalEntryId = journalEntryId;
        SetUpdated(userId);
    }

    public void RegisterAppliedSupplierNote(string noteType, decimal noteTotalAmount, Guid userId)
    {
        if (noteTotalAmount <= 0)
            throw new ArgumentException("Note amount must be greater than zero.", nameof(noteTotalAmount));
        if (Status != ExpenseStatus.Approved)
            throw new InvalidOperationException("Notes can only be applied to Approved expenses.");

        var nt = (noteType ?? string.Empty).Trim().ToUpperInvariant();
        if (nt == "CREDIT")
        {
            var sum = TotalNotesApplied + noteTotalAmount;
            if (sum > Total + TotalTolerance)
                throw new InvalidOperationException(
                    $"Applied credit notes ({sum:F2}) exceed the expense total ({Total:F2}).");
            TotalNotesApplied = sum;
        }
        else if (nt != "DEBIT")
            throw new ArgumentException("noteType must be CREDIT or DEBIT.", nameof(noteType));

        SetUpdated(userId);
    }

    public void Reject(Guid userId, string reason)
    {
        if (Status == ExpenseStatus.Approved)
            throw new InvalidOperationException("An approved expense cannot be rejected.");
        if (Status == ExpenseStatus.Rejected)
            throw new InvalidOperationException("Expense is already rejected.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        Status          = ExpenseStatus.Rejected;
        RejectedBy      = userId;
        RejectedAt      = DateTime.UtcNow;
        RejectionReason = reason.Trim();
        SetUpdated(userId);
    }

    private static void ValidateAmounts(decimal subtotal, decimal taxTotal, decimal total)
    {
        if (subtotal < 0 || taxTotal < 0 || total < 0)
            throw new ArgumentException("Amounts cannot be negative.");

        var calculated = subtotal + taxTotal;
        if (Math.Abs(calculated - total) > TotalTolerance)
            throw new ArgumentException($"Subtotal + TaxTotal ({calculated:F2}) does not match Total ({total:F2}).");
    }

    private static void ValidateAccessKey(string accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
            throw new ArgumentException("Access key is required for XML expenses.", nameof(accessKey));
        var c = accessKey.Trim();
        if (c.Length != AccessKeyLen || !c.All(char.IsDigit))
            throw new ArgumentException("Access key must be 49 numeric digits.");
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
