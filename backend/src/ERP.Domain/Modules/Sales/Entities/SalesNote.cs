using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesNote : AuditableEntity, ISubscriberScopedEntity
{
    public const int NoteTypeMaxLen   = 20;
    public const int ReasonMaxLen     = 300;
    public const int DocTypeMaxLen    = 10;
    public const int EstabMaxLen      = 3;
    public const int EmPointMaxLen    = 3;
    public const int SequentialMaxLen = 9;
    public const int AccessKeyMaxLen  = 49;
    public const int StatusMaxLen     = 20;
    public const int XmlPathMaxLen    = 500;
    public const int ErrorMaxLen      = 1000;

    private readonly List<SalesNoteLine> _lines = new();

    public Guid      OriginalBillId  { get; private set; }
    public string    NoteType        { get; private set; } = null!;
    public string    Reason          { get; private set; } = null!;
    public string    DocType         { get; private set; } = null!;
    public string    EstabCode       { get; private set; } = null!;
    public string    EmPointCode     { get; private set; } = null!;
    public string    Sequential      { get; private set; } = null!;
    public string    AccessKey       { get; private set; } = null!;
    public DateTime  IssueDate       { get; private set; }
    public decimal   Subtotal        { get; private set; }
    public decimal   VatTotal        { get; private set; }
    public decimal   Total           { get; private set; }
    public string    Status          { get; private set; } = "Draft";
    public string?   XmlSignedPath   { get; private set; }
    public string?   XmlAuthPath     { get; private set; }
    public string?   AuthNumber      { get; private set; }
    public DateTime? AuthDate        { get; private set; }
    public string?   ErrorMessage    { get; private set; }
    public Guid?     JournalEntryId  { get; private set; }

    public SalesBill OriginalBill { get; private set; } = null!;
    public IReadOnlyList<SalesNoteLine> Lines => _lines.AsReadOnly();

    private SalesNote() { }

    public static SalesNote Create(
        Guid     subscriberId,
        Guid     originalBillId,
        string   noteType,
        string   reason,
        string   docType,
        string   estabCode,
        string   emPointCode,
        string   sequential,
        string   accessKey,
        DateTime issueDate,
        Guid     createdBy)
    {
        var nt = (noteType ?? string.Empty).Trim().ToUpperInvariant();
        if (nt is not ("CREDIT" or "DEBIT"))
            throw new ArgumentException("NoteType must be CREDIT or DEBIT.", nameof(noteType));

        var dt = (docType ?? string.Empty).Trim();
        if (dt is not ("04" or "05"))
            throw new ArgumentException("DocType must be '04' (credit) or '05' (debit). Caller must resolve from NoteType before invoking Create.", nameof(docType));

        var note = new SalesNote
        {
            Id             = Guid.NewGuid(),
            SubscriberId       = subscriberId,
            OriginalBillId = originalBillId,
            NoteType       = nt,
            Reason         = (reason ?? string.Empty).Trim(),
            DocType        = dt,
            EstabCode      = estabCode.Trim(),
            EmPointCode    = emPointCode.Trim(),
            Sequential     = sequential.Trim(),
            AccessKey      = accessKey.Trim(),
            IssueDate      = issueDate,
            Status         = "Draft",
        };
        note.SetCreated(createdBy);
        return note;
    }

    public void Validate(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft notes can be validated (current: {Status}).");
        Status = "Validated";
        SetUpdated(userId);
    }

    public void AddLine(SalesNoteLine line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
        RecalcTotals();
    }

    private void RecalcTotals()
    {
        Subtotal = _lines.Sum(d => d.Subtotal);
        VatTotal = _lines.Sum(d => d.VatTotal);
        Total    = _lines.Sum(d => d.Total);
    }

    public void AuthorizeCreditNote(
        Guid     userId,
        Guid     warehouseId,
        string   authNumber,
        DateTime authDate,
        string?  xmlSignedPath,
        string?  xmlAuthPath,
        Guid     journalEntryId,
        IReadOnlyList<SalesNoteStockLine>? stockLines = null,
        Guid? companyId = null)
    {
        if (Status != "Validated")
            throw new InvalidOperationException($"Only Validated notes can be authorized (current: {Status}).");
        if (!string.Equals(NoteType, "CREDIT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AuthorizeCreditNote is only for CREDIT notes.");

        Status         = "Authorized";
        AuthNumber     = authNumber;
        AuthDate       = authDate;
        XmlSignedPath  = string.IsNullOrWhiteSpace(xmlSignedPath) ? null : xmlSignedPath;
        XmlAuthPath    = string.IsNullOrWhiteSpace(xmlAuthPath) ? null : xmlAuthPath;
        JournalEntryId = journalEntryId;
        SetUpdated(userId);

        var lines = stockLines ?? Array.Empty<SalesNoteStockLine>();
        if (lines.Count > 0)
        {
            var number = $"{EstabCode}-{EmPointCode}-{Sequential}";
            RaiseDomainEvent(new SalesNoteAuthorizedEvent(Id, SubscriberId, userId, warehouseId, companyId ?? Guid.Empty, number, lines));
        }
    }

    public void AuthorizeDebitNote(
        Guid     userId,
        string   authNumber,
        DateTime authDate,
        string?  xmlSignedPath,
        string?  xmlAuthPath,
        Guid     journalEntryId)
    {
        if (Status != "Validated")
            throw new InvalidOperationException($"Only Validated notes can be authorized (current: {Status}).");
        if (!string.Equals(NoteType, "DEBIT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AuthorizeDebitNote is only for DEBIT notes.");

        Status         = "Authorized";
        AuthNumber     = authNumber;
        AuthDate       = authDate;
        XmlSignedPath  = string.IsNullOrWhiteSpace(xmlSignedPath) ? null : xmlSignedPath;
        XmlAuthPath    = string.IsNullOrWhiteSpace(xmlAuthPath) ? null : xmlAuthPath;
        JournalEntryId = journalEntryId;
        SetUpdated(userId);
    }

    public void Reject(Guid userId, string errorMessage)
    {
        Status       = "Rejected";
        ErrorMessage = errorMessage?.Trim();
        SetUpdated(userId);
    }

    public void MarkSendError(Guid userId, string errorMessage)
    {
        Status       = "SendError";
        ErrorMessage = errorMessage?.Trim();
        SetUpdated(userId);
    }
}
