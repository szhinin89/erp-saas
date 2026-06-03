using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseWithholding : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public const int AccessKeyMaxLen  = 49;
    public const int EstabMaxLen      = 3;
    public const int EmPointMaxLen    = 3;
    public const int SequentialMaxLen = 9;
    public const int StatusMaxLen     = 30;
    public const int XmlPathMaxLen    = 500;
    public const int ErrorMaxLen      = 500;

    private readonly List<PurchaseWithholdingLine> _lines = new();

    public Guid CompanyId { get; private set; }
    public Guid                 BusinessPartnerId    { get; private set; }
    public WithholdingDirection Direction            { get; private set; } = WithholdingDirection.Issued;
    public Guid?                PurchaseDocumentId   { get; private set; }
    public string               VoucherType          { get; private set; } = "RETENTION";
    public string               AccessKey            { get; private set; } = null!;
    public DateTime             IssueDate            { get; private set; }
    public string?              EstabCode            { get; private set; }
    public string?              EmPointCode          { get; private set; }
    public string?              Sequential           { get; private set; }
    public decimal              TotalRetained        { get; private set; }
    public string               Status               { get; private set; } = "Draft";
    public string?              XmlPath              { get; private set; }
    public string?              XmlSignedPath        { get; private set; }
    public string?              XmlAuthPath          { get; private set; }
    public string?              AuthNumber           { get; private set; }
    public DateTime?            AuthDate             { get; private set; }
    public string?              ErrorMessage         { get; private set; }
    public Guid?                JournalEntryId       { get; private set; }

    public PurchaseDocument? PurchaseDocument { get; private set; }
    public IReadOnlyList<PurchaseWithholdingLine> Lines => _lines.AsReadOnly();

    private PurchaseWithholding() { }

    public static PurchaseWithholding Rehydrate() => new();

    public void AddLine(PurchaseWithholdingLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
        TotalRetained = _lines.Sum(l => l.AmountRetained);
    }
}
