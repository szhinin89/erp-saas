using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Events;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchBill : AuditableEntity, ISubscriberScopedEntity
{
    public const int InvoiceNumberMaxLen = 50;
    public const int AccessKeyLen        = 49;
    public const int PaymentTermsMaxLen  = 30;
    public const int NotesMaxLen         = 1000;
    public const int RejectionReasonMaxLen = 500;
    public const int XmlPathMaxLen       = 500;
    public const decimal TotalTolerance  = 0.01m;

    private readonly List<PurchBillLine> _lines = new();

    public Guid           BusinessPartnerId { get; private set; }
    public string         InvoiceNumber     { get; private set; } = null!;
    public string?        AccessKey         { get; private set; }
    public string?        XmlPath           { get; private set; }
    public DateTime       InvoiceDate       { get; private set; }
    public DateTime?      DueDate           { get; private set; }
    public PurchaseStatus Status            { get; private set; } = PurchaseStatus.Draft;
    public string         PaymentTerms      { get; private set; } = null!;
    public decimal        Subtotal          { get; private set; }
    public decimal        VatTotal          { get; private set; }
    public decimal        Total             { get; private set; }
    public string?        Notes             { get; private set; }
    public Guid?          ValidatedBy       { get; private set; }
    public DateTime?      ValidatedAt       { get; private set; }
    public Guid?          ApprovedBy        { get; private set; }
    public DateTime?      ApprovedAt        { get; private set; }
    public Guid?          RejectedBy        { get; private set; }
    public DateTime?      RejectedAt        { get; private set; }
    public string?        RejectionReason   { get; private set; }
    public Guid?          JournalEntryId    { get; private set; }
    public decimal        TotalNotesApplied { get; private set; }

    public IReadOnlyList<PurchBillLine> Lines => _lines.AsReadOnly();

    private PurchBill() { }

    public static PurchBill Create(
        Guid      subscriberId,
        Guid      businessPartnerId,
        string    invoiceNumber,
        string?   accessKey,
        string?   xmlPath,
        DateTime  invoiceDate,
        DateTime? dueDate,
        string    paymentTerms,
        string?   notes,
        Guid      createdBy)
    {
        var b = new PurchBill
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            BusinessPartnerId = businessPartnerId,
            InvoiceNumber     = invoiceNumber.Trim(),
            AccessKey     = string.IsNullOrWhiteSpace(accessKey) ? null : accessKey.Trim(),
            XmlPath       = string.IsNullOrWhiteSpace(xmlPath) ? null : xmlPath.Trim(),
            InvoiceDate   = invoiceDate,
            DueDate       = dueDate,
            PaymentTerms  = paymentTerms,
            Notes         = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status        = PurchaseStatus.Draft,
        };
        b.SetCreated(createdBy);
        return b;
    }

    public void AddLine(
        string  description,
        string? supplierProductCode,
        Guid?   productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountPct,
        decimal vatPct,
        Guid    createdBy)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Lines can only be added to a Draft bill.");

        _lines.Add(PurchBillLine.Create(
            SubscriberId, Id, description, supplierProductCode, productId,
            quantity, unitPrice, discountPct, vatPct, createdBy));

        RecalculateTotals();
    }

    public void Validate(Guid userId)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Only Draft bills can be validated.");
        if (_lines.Count == 0)
            throw new InvalidOperationException("Bill must have at least one line.");

        Status      = PurchaseStatus.Validated;
        ValidatedBy = userId;
        ValidatedAt = DateTime.UtcNow;
        SetUpdated(userId);
    }

    public void Approve(
        Guid userId,
        Guid? journalEntryId,
        IReadOnlyList<PurchBillApprovedStockLine> stockLines)
    {
        if (Status != PurchaseStatus.Validated)
            throw new InvalidOperationException("Only Validated bills can be approved.");

        Status         = PurchaseStatus.Approved;
        ApprovedBy     = userId;
        ApprovedAt     = DateTime.UtcNow;
        JournalEntryId = journalEntryId;
        SetUpdated(userId);

        RaiseDomainEvent(new PurchBillApprovedEvent(Id, SubscriberId, InvoiceNumber, userId, stockLines));
    }

    public void RegisterAppliedNote(string noteType, decimal noteTotalAmount, Guid userId)
    {
        if (noteTotalAmount <= 0)
            throw new ArgumentException("Note amount must be greater than zero.", nameof(noteTotalAmount));
        if (Status != PurchaseStatus.Approved)
            throw new InvalidOperationException("Notes can only be applied to Approved bills.");

        var nt = (noteType ?? string.Empty).Trim().ToUpperInvariant();
        if (nt == "CREDIT")
        {
            var sum = TotalNotesApplied + noteTotalAmount;
            if (sum > Total + TotalTolerance)
                throw new InvalidOperationException(
                    $"Applied credit notes ({sum:F2}) exceed the bill total ({Total:F2}).");
            TotalNotesApplied = sum;
        }
        else if (nt != "DEBIT")
            throw new ArgumentException("noteType must be CREDIT or DEBIT.", nameof(noteType));

        SetUpdated(userId);
    }

    public void Reject(Guid userId, string reason)
    {
        if (Status == PurchaseStatus.Approved)
            throw new InvalidOperationException("An approved bill cannot be rejected.");
        if (Status == PurchaseStatus.Rejected)
            throw new InvalidOperationException("Bill is already rejected.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        Status          = PurchaseStatus.Rejected;
        RejectedBy      = userId;
        RejectedAt      = DateTime.UtcNow;
        RejectionReason = reason.Trim();
        SetUpdated(userId);
    }

    private void RecalculateTotals()
    {
        Subtotal = _lines.Sum(d => d.Subtotal);
        VatTotal = _lines.Sum(d => d.VatAmount);
        Total    = Subtotal + VatTotal;
    }
}
