using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class IssuedRetention : AuditableEntity, ISubscriberScopedEntity
{
    public const int AccessKeyLen    = 49;
    public const int EstabMaxLen     = 3;
    public const int EmPointMaxLen   = 3;
    public const int SequentialMaxLen = 9;
    public const int StatusMaxLen    = 20;
    public const int XmlPathMaxLen   = 500;
    public const int ErrorMaxLen     = 1000;

    private readonly List<PurchRetentionLine> _lines = new();

    public Guid      BusinessPartnerId   { get; private set; }
    public Guid?     PurchBillId         { get; private set; }
    public string    VoucherType         { get; private set; } = "RETENTION";
    public string    AccessKey           { get; private set; } = null!;
    public DateTime  IssueDate           { get; private set; }
    public string    EstablishmentCode   { get; private set; } = null!;
    public string    EmissionPointCode   { get; private set; } = null!;
    public string    Sequential          { get; private set; } = null!;
    public string    Status              { get; private set; } = "Draft";
    public string?   XmlSignedPath       { get; private set; }
    public string?   XmlAuthPath         { get; private set; }
    public string?   AuthNumber          { get; private set; }
    public DateTime? AuthDate            { get; private set; }
    public string?   ErrorMessage        { get; private set; }
    public Guid?     JournalEntryId      { get; private set; }
    public decimal   TotalRetained       { get; private set; }

    public IReadOnlyList<PurchRetentionLine> Lines => _lines.AsReadOnly();

    private IssuedRetention() { }

    public static IssuedRetention Create(
        Guid     subscriberId,
        Guid     businessPartnerId,
        Guid?    purchBillId,
        string   accessKey,
        DateTime issueDate,
        string   establishmentCode,
        string   emissionPointCode,
        string   sequential,
        Guid     createdBy)
    {
        var r = new IssuedRetention
        {
            Id                = Guid.NewGuid(),
            SubscriberId      = subscriberId,
            BusinessPartnerId = businessPartnerId,
            PurchBillId       = purchBillId,
            AccessKey         = accessKey.Trim(),
            IssueDate         = issueDate,
            EstablishmentCode = establishmentCode.Trim(),
            EmissionPointCode = emissionPointCode.Trim(),
            Sequential        = sequential.Trim(),
            Status            = "Draft",
            TotalRetained     = 0,
        };
        r.SetCreated(createdBy);
        return r;
    }

    public void AddLine(PurchRetentionLine line)
    {
        if (line is null) throw new ArgumentNullException(nameof(line));
        _lines.Add(line);
        TotalRetained = _lines.Sum(d => d.AmountRetained);
    }

    public void Validate(Guid userId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Only Draft retentions can be validated (current: {Status}).");
        Status = "Validated";
        SetUpdated(userId);
    }

    public void Authorize(
        Guid     userId,
        string   authNumber,
        DateTime authDate,
        string?  xmlSignedPath,
        string?  xmlAuthPath,
        Guid     journalEntryId)
    {
        if (Status != "Validated")
            throw new InvalidOperationException($"Only Validated retentions can be authorized (current: {Status}).");
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
}
