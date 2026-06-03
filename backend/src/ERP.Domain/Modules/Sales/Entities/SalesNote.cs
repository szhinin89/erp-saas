using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Events;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesNote : AuditableEntity, ICompanyOperationalEntity
{
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

    /// <summary>Empresa operativa. Null = registro legacy pre-migraciÃ³n company-scope.</summary>
    public Guid CompanyId { get; private set; }
    public Guid      OriginalBillId  { get; private set; }
    public NoteType  NoteType        { get; private set; }
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
        NoteType noteType,
        string   reason,
        string   docType,
        string   estabCode,
        string   emPointCode,
        string   sequential,
        string   accessKey,
        DateTime issueDate,
        Guid     createdBy,
        Guid companyId = default)
    {
        var dt = (docType ?? string.Empty).Trim();
        if (dt is not ("04" or "05"))
            throw new ArgumentException("DocType must be '04' (credit) or '05' (debit).", nameof(docType));

        var note = new SalesNote
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            CompanyId = companyId,
            OriginalBillId = originalBillId,
            NoteType       = noteType,
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
        Guid companyId = default)
    {
        if (Status != "Validated")
            throw new InvalidOperationException($"Only Validated notes can be authorized (current: {Status}).");
        if (NoteType != NoteType.Credit)
            throw new InvalidOperationException("AuthorizeCreditNote is only for Credit notes.");

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
            RaiseDomainEvent(new SalesNoteAuthorizedEvent(Id, SubscriberId, userId, warehouseId, companyId, number, lines));
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
        if (NoteType != NoteType.Debit)
            throw new InvalidOperationException("AuthorizeDebitNote is only for Debit notes.");

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
