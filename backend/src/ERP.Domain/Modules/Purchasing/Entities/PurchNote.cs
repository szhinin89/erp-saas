using ERP.Domain.Common;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Purchasing.Events;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchNote : AuditableEntity, ITenantEntity
{
    public const int NoteTypeMaxLen     = 20;
    public const int ReasonMaxLen       = 300;
    public const int AccessKeyMaxLen    = 49;
    public const int StatusMaxLen       = 20;
    public const int XmlPathMaxLen      = 500;
    public const int EstabMaxLen        = 3;
    public const int EmPointMaxLen      = 3;
    public const int SequentialMaxLen   = 9;
    public const int AuthNumberMaxLen   = 64;

    private readonly List<PurchNoteLine> _lines = new();

    public Guid      SupplierId       { get; private set; }
    public Guid?     PurchBillId      { get; private set; }
    public Guid?     ExpenseInvoiceId { get; private set; }
    public string    NoteType         { get; private set; } = null!;
    public string    Reason           { get; private set; } = null!;
    public string    AccessKey        { get; private set; } = null!;
    public DateTime  IssueDate        { get; private set; }
    public string    EstabCode        { get; private set; } = null!;
    public string    EmPointCode      { get; private set; } = null!;
    public string    Sequential       { get; private set; } = null!;
    public decimal   Subtotal         { get; private set; }
    public decimal   VatTotal         { get; private set; }
    public decimal   Total            { get; private set; }
    public string    Status           { get; private set; } = "Draft";
    public string?   XmlPath          { get; private set; }
    public string?   AuthNumber       { get; private set; }
    public DateTime? AuthDate         { get; private set; }
    public Guid?     JournalEntryId   { get; private set; }

    public Supplier         Supplier    { get; private set; } = null!;
    public PurchBill?       PurchBill   { get; private set; }
    public ExpenseInvoice?  ExpenseInv  { get; private set; }
    public IReadOnlyList<PurchNoteLine> Lines => _lines.AsReadOnly();

    private PurchNote() { }

    public static PurchNote Create(
        Guid     tenantId,
        Guid     supplierId,
        Guid?    purchBillId,
        Guid?    expenseInvoiceId,
        string   noteType,
        string   reason,
        string   accessKey,
        DateTime issueDate,
        string   estabCode,
        string   emPointCode,
        string   sequential,
        Guid     createdBy)
    {
        if (purchBillId.HasValue && expenseInvoiceId.HasValue)
            throw new ArgumentException("Note cannot be linked to both a bill and an expense.");

        var nt = (noteType ?? string.Empty).Trim().ToUpperInvariant();
        if (nt is not ("CREDIT" or "DEBIT"))
            throw new ArgumentException("NoteType must be CREDIT or DEBIT.", nameof(noteType));

        var n = new PurchNote
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            SupplierId       = supplierId,
            PurchBillId      = purchBillId,
            ExpenseInvoiceId = expenseInvoiceId,
            NoteType         = nt,
            Reason           = (reason ?? string.Empty).Trim(),
            AccessKey        = (accessKey ?? string.Empty).Trim(),
            IssueDate        = issueDate,
            EstabCode        = estabCode.Trim(),
            EmPointCode      = emPointCode.Trim(),
            Sequential       = sequential.Trim(),
            Status           = "Draft",
        };
        n.SetCreated(createdBy);
        return n;
    }

    public void AddLine(PurchNoteLine line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        line.AssignNoteId(Id);
        _lines.Add(line);
        RecalcTotals();
    }

    private void RecalcTotals()
    {
        Subtotal = _lines.Sum(d => d.Subtotal);
        VatTotal = _lines.Sum(d => d.VatAmount);
        Total    = _lines.Sum(d => d.Total);
    }

    public void SetXmlPath(string? path, Guid userId)
    {
        XmlPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        SetUpdated(userId);
    }

    public void LinkToBill(Guid purchBillId, Guid userId)
    {
        if (ExpenseInvoiceId.HasValue)
            throw new InvalidOperationException("Note is already linked to an expense.");
        PurchBillId = purchBillId;
        SetUpdated(userId);
    }

    public void LinkToExpense(Guid expenseInvoiceId, Guid userId)
    {
        if (PurchBillId.HasValue)
            throw new InvalidOperationException("Note is already linked to a purchase bill.");
        ExpenseInvoiceId = expenseInvoiceId;
        SetUpdated(userId);
    }

    public void Approve(
        Guid    userId,
        Guid?   journalEntryId,
        string? authNumber,
        DateTime? authDate,
        IReadOnlyList<PurchNoteStockLine>? stockLines = null)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft notes can be approved (current: {Status}).");
        if (!PurchBillId.HasValue && !ExpenseInvoiceId.HasValue)
            throw new InvalidOperationException("Note must be linked to a bill or expense before approval.");

        Status         = "Approved";
        JournalEntryId = journalEntryId;
        AuthNumber     = string.IsNullOrWhiteSpace(authNumber) ? null : authNumber.Trim();
        AuthDate       = authDate;
        SetUpdated(userId);

        var lines = stockLines ?? Array.Empty<PurchNoteStockLine>();
        if (PurchBillId.HasValue && lines.Count > 0)
        {
            var number = $"{EstabCode}-{EmPointCode}-{Sequential}";
            RaiseDomainEvent(new PurchNoteApprovedEvent(
                Id, TenantId, userId, PurchBillId.Value, NoteType, number, lines));
        }
    }
}
