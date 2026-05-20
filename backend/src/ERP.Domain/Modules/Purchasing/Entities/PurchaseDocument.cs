using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Enums;
namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchaseDocument : AuditableEntity, ITenantEntity
{
    public const int DocNumberMaxLen = 30;
    public const int AccessKeyLen    = 49;
    public const int NotesMaxLen     = 1000;

    private readonly List<PurchaseDetail> _lines = new();

    public Guid?                CompanyId           { get; private set; }
    public Guid?                SupplierId          { get; private set; }
    public PurchaseDocumentType DocType             { get; private set; } = PurchaseDocumentType.Invoice;
    public string               DocNumber           { get; private set; } = null!;
    public string?              AccessKey           { get; private set; }
    public DateTime             IssueDate           { get; private set; }
    public DateTime?            RequiredDate        { get; private set; }
    public DateTime?            DueDate             { get; private set; }
    public decimal              Subtotal            { get; private set; }
    public decimal              VatTotal            { get; private set; }
    public decimal              Total               { get; private set; }
    public decimal              TotalNotesApplied   { get; private set; }
    public string               Currency            { get; private set; } = "USD";
    public string               Status              { get; private set; } = "Draft";
    public string?              PaymentTerms        { get; private set; }
    public string?              Notes               { get; private set; }
    public string?              XmlPath             { get; private set; }
    public Guid?                ReferenceDocumentId { get; private set; }
    public string?              Reason              { get; private set; }
    public Guid?                JournalEntryId      { get; private set; }
    public Guid?                ValidatedBy         { get; private set; }
    public DateTime?            ValidatedAt         { get; private set; }
    public Guid?                ApprovedBy          { get; private set; }
    public DateTime?            ApprovedAt          { get; private set; }
    public Guid?                RejectedBy          { get; private set; }
    public DateTime?            RejectedAt          { get; private set; }
    public string?              RejectionReason     { get; private set; }

    public PurchaseElectronicDoc? Electronic { get; private set; }
    public PurchaseDocument?      Reference   { get; private set; }
    public IReadOnlyList<PurchaseDetail> Lines => _lines.AsReadOnly();

    private PurchaseDocument() { }

    /// <summary>Rehidratación desde capa de persistencia / mapper.</summary>
    public static PurchaseDocument Rehydrate() => new();

    public void AddLine(PurchaseDetail line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
    }
}
