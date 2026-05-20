using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesWithholding : AuditableEntity, ISubscriberScopedEntity
{
    public const int VoucherTypeMaxLen = 30;
    public const int AccessKeyMaxLen   = 49;
    public const int StatusMaxLen      = 30;
    public const int XmlPathMaxLen     = 500;
    public const int ErrorMaxLen       = 500;
    public const int IssuerRucMaxLen   = 13;
    public const int IssuerNameMaxLen  = 200;

    private readonly List<SalesWithholdingLine> _lines = new();

    public Guid?                 CompanyId         { get; private set; }
    public Guid?                 CustomerId        { get; private set; }
    public WithholdingDirection  Direction         { get; private set; } = WithholdingDirection.Received;
    public Guid?                 SalesDocumentId   { get; private set; }
    public string?               IssuerRuc         { get; private set; }
    public string?               IssuerName        { get; private set; }
    public string                VoucherType       { get; private set; } = "RETENTION";
    public string                AccessKey         { get; private set; } = null!;
    public DateTime              IssueDate         { get; private set; }
    public string?               EstabCode         { get; private set; }
    public string?               EmPointCode       { get; private set; }
    public string?               Sequential        { get; private set; }
    public decimal               TotalRetained     { get; private set; }
    public string                Status            { get; private set; } = "Active";
    public string?               XmlPath           { get; private set; }
    public string?               XmlSignedPath     { get; private set; }
    public string?               XmlAuthPath       { get; private set; }
    public string?               AuthNumber        { get; private set; }
    public DateTime?             AuthDate          { get; private set; }
    public string?               ErrorMessage      { get; private set; }
    public Guid?                 JournalEntryId    { get; private set; }

    public Customer? Customer { get; private set; }
    public SalesDocument? SalesDocument { get; private set; }
    public IReadOnlyList<SalesWithholdingLine> Lines => _lines.AsReadOnly();

    private SalesWithholding() { }

    public static SalesWithholding Rehydrate() => new();

    public void AddLine(SalesWithholdingLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
        TotalRetained = _lines.Sum(l => l.AmountRetained);
    }
}
