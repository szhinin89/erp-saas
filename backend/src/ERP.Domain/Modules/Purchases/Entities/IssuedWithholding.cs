using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class IssuedWithholding
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    public const int NumberMaxLen = 20;
    public const int AccessKeyLen = 49;
    public const int CancelReasonMaxLen = 300;
    public const int SriMessageMaxLen = 500;
    public const int PathMaxLen = 500;
    public const int SriReceiptMaxLen = 50;

    // ── Core ────────────────────────────────────────────────────────────
    public Guid PurchaseInvoiceId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid EmissionPointId { get; private set; }
    public string WithholdingNumber { get; private set; } = null!;
    public DateOnly IssueDate { get; private set; }
    public decimal TotalRetainedVat { get; private set; }
    public decimal TotalRetainedIncome { get; private set; }
    public decimal TotalRetainedIsd { get; private set; }
    public decimal TotalRetained { get; private set; }
    public WithholdingStatus Status { get; private set; } = WithholdingStatus.Draft;

    // ── Cancellation ────────────────────────────────────────────────────
    public string? CancelReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }

    // ── SRI Electronic Document ─────────────────────────────────────────
    public string? AccessKey { get; private set; }
    public string? XmlPath { get; private set; }
    public string? SignedXmlPath { get; private set; }
    public string? PdfPath { get; private set; }
    public SriDocumentStatus SriStatus { get; private set; } = SriDocumentStatus.None;
    public string? SriReceiptNumber { get; private set; }
    public string? SriAuthorizationNumber { get; private set; }
    public DateTime? SriAuthorizationDate { get; private set; }
    public string? SriMessage { get; private set; }

    // ── Details ─────────────────────────────────────────────────────────
    private readonly List<IssuedWithholdingDetail> _details = new();
    public IReadOnlyList<IssuedWithholdingDetail> Details => _details.AsReadOnly();

    private IssuedWithholding() { }

    public static IssuedWithholding CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid purchaseInvoiceId,
        Guid supplierId,
        Guid emissionPointId,
        DateOnly issueDate,
        Guid createdBy
    )
    {
        var w = new IssuedWithholding
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            PurchaseInvoiceId = purchaseInvoiceId,
            SupplierId = supplierId,
            EmissionPointId = emissionPointId,
            IssueDate = issueDate,
            Status = WithholdingStatus.Draft,
            SriStatus = SriDocumentStatus.None,
        };
        w.SetCreated(createdBy);
        return w;
    }

    public void AddDetail(IssuedWithholdingDetail detail)
    {
        EnsureEditable();
        _details.Add(detail);
        RecalcTotals();
    }

    public void ReplaceDetails(IEnumerable<IssuedWithholdingDetail> details)
    {
        EnsureEditable();
        _details.Clear();
        foreach (var d in details)
            _details.Add(d);
        RecalcTotals();
    }

    public void Issue(string withholdingNumber, Guid updatedBy)
    {
        EnsureEditable();
        if (_details.Count == 0)
            throw new InvalidOperationException("No se puede emitir una retención sin líneas.");
        if (string.IsNullOrWhiteSpace(withholdingNumber))
            throw new ArgumentException(
                "El número de retención es obligatorio.",
                nameof(withholdingNumber)
            );

        WithholdingNumber = withholdingNumber.Trim();
        Status = WithholdingStatus.Issued;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new IssuedWithholdingIssuedEvent(
                TenantId,
                Id,
                PurchaseInvoiceId,
                SupplierId,
                WithholdingNumber,
                TotalRetained
            )
        );
    }

    public void Cancel(string reason, Guid cancelledBy)
    {
        if (Status != WithholdingStatus.Issued)
            throw new InvalidOperationException("Solo se pueden anular retenciones emitidas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        Status = WithholdingStatus.Cancelled;
        CancelReason = reason.Trim();
        CancelledAt = DateTime.UtcNow;
        CancelledBy = cancelledBy;
        SriStatus = SriDocumentStatus.Voided;
        SetUpdated(cancelledBy);
        RaiseDomainEvent(
            new IssuedWithholdingCancelledEvent(
                TenantId,
                Id,
                PurchaseInvoiceId,
                SupplierId,
                WithholdingNumber,
                TotalRetained,
                CancelReason
            )
        );
    }

    // ── SRI Electronic Document Lifecycle ───────────────────────────────

    public void SetAccessKey(string accessKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey) || accessKey.Trim().Length != AccessKeyLen)
            throw new ArgumentException(
                $"La clave de acceso debe tener {AccessKeyLen} dígitos.",
                nameof(accessKey)
            );
        AccessKey = accessKey.Trim();
    }

    public void MarkSigned(string signedXmlPath, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(signedXmlPath))
            throw new ArgumentException(
                "La ruta del XML firmado es obligatoria.",
                nameof(signedXmlPath)
            );
        SignedXmlPath = signedXmlPath.Trim();
        SriStatus = SriDocumentStatus.Signed;
        SetUpdated(updatedBy);
    }

    public void MarkAuthorized(
        string authorizationNumber,
        DateTime authorizationDate,
        Guid updatedBy
    )
    {
        if (string.IsNullOrWhiteSpace(authorizationNumber))
            throw new ArgumentException(
                "El número de autorización SRI es obligatorio.",
                nameof(authorizationNumber)
            );
        SriAuthorizationNumber = authorizationNumber.Trim();
        SriAuthorizationDate = authorizationDate;
        SriStatus = SriDocumentStatus.Authorized;
        SetUpdated(updatedBy);
    }

    public void MarkRejected(string message, Guid updatedBy)
    {
        SriMessage = message?.Trim();
        SriStatus = SriDocumentStatus.Rejected;
        SetUpdated(updatedBy);
    }

    public void SetXmlPath(string xmlPath) => XmlPath = xmlPath?.Trim();

    public void SetPdfPath(string pdfPath) => PdfPath = pdfPath?.Trim();

    public void SetSriReceiptNumber(string rn) => SriReceiptNumber = rn?.Trim();

    // ── Private ─────────────────────────────────────────────────────────

    private void EnsureEditable()
    {
        if (Status != WithholdingStatus.Draft)
            throw new InvalidOperationException(
                "Solo se pueden editar retenciones en estado borrador."
            );
    }

    private void RecalcTotals()
    {
        TotalRetainedVat = _details.Where(d => d.TaxType == "IVA").Sum(d => d.AmountRetained);
        TotalRetainedIncome = _details.Where(d => d.TaxType == "RENTA").Sum(d => d.AmountRetained);
        TotalRetainedIsd = _details.Where(d => d.TaxType == "ISD").Sum(d => d.AmountRetained);
        TotalRetained = TotalRetainedVat + TotalRetainedIncome + TotalRetainedIsd;
    }
}
