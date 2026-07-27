using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

/// <summary>
/// Documento electrónico de recepción de compras persistido — Fase 2 de PurchaseReception.
/// Representa un comprobante recibido (por ahora solo Facturas, vía TXT) pendiente de
/// procesamiento; nunca crea ni modifica el agregado <c>PurchaseInvoice</c> directamente —
/// <see cref="PurchaseId"/> es solo una referencia de solo lectura hacia una compra ya existente
/// o futura, asignada por una fase posterior (conciliación/creación).
/// Branch Ownership Rule: TenantId+CompanyId+BranchId, BranchId nunca viene del cliente.
/// </summary>
public sealed class PurchaseReceptionDocument : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int SupplierRucMaxLen         = 20;
    public const int SupplierNameMaxLen        = 200;
    public const int AccessKeyMaxLen           = 49;
    public const int InvoiceNumberMaxLen       = 30;
    public const int AuthorizationNumberMaxLen = 49;

    public Guid CompanyId { get; private set; }

    /// <summary>Sucursal receptora — asignada exclusivamente desde <c>ICurrentBranch</c> en el handler, inmutable.</summary>
    public Guid BranchId { get; private set; }

    /// <summary>Tipo de comprobante de origen — Fase 2 solo persiste <c>Invoice</c> (Factura).</summary>
    public PurchaseReceptionSourceDocType SourceDocType { get; private set; }

    public string SupplierRuc  { get; private set; } = null!;
    public string SupplierName { get; private set; } = null!;

    /// <summary>BusinessPartner ya resuelto como proveedor, si existía al momento de importar.</summary>
    public Guid? SupplierId { get; private set; }

    public string   AccessKey         { get; private set; } = null!;
    public string   InvoiceNumber     { get; private set; } = null!;
    public DateOnly IssueDate         { get; private set; }

    /// <summary>Fecha de autorización — inicialmente la del TXT; se sobrescribe con la del SRI al descargar el XML.</summary>
    public DateTime? AuthorizationDate { get; private set; }

    /// <summary>Número de autorización SRI — null hasta que se consulta/descarga el XML autorizado (Fase 3).</summary>
    public string? AuthorizationNumber { get; private set; }

    /// <summary>Momento en que se descargó/almacenó el XML autorizado — null mientras no se haya consultado el SRI.</summary>
    public DateTime? XmlDownloadedAt { get; private set; }

    public decimal Subtotal    { get; private set; }
    public decimal VatAmount   { get; private set; }
    public decimal TotalAmount { get; private set; }

    public PurchaseReceptionDocumentStatus Status { get; private set; }

    /// <summary>Compra del ERP vinculada a este documento — null hasta que una fase futura la cree/enlace.</summary>
    public Guid? PurchaseId { get; private set; }

    /// <summary>XML original del comprobante — reservado para la futura descarga/parsing SRI. Null en Fase 2.</summary>
    public string? XmlContent { get; private set; }

    private readonly List<PurchaseReceptionLine> _lines = new();
    /// <summary>Líneas de detalle extraídas del XML autorizado — pobladas por <see cref="AttachSriAuthorization"/>, vacío hasta entonces.</summary>
    public IReadOnlyList<PurchaseReceptionLine> Lines => _lines.AsReadOnly();

    private PurchaseReceptionDocument() { }

    public static PurchaseReceptionDocument Create(
        Guid tenantId, Guid companyId, Guid branchId,
        PurchaseReceptionSourceDocType sourceDocType,
        string supplierRuc, string supplierName, Guid? supplierId,
        string accessKey, string invoiceNumber, DateOnly issueDate, DateTime? authorizationDate,
        decimal subtotal, decimal vatAmount, decimal totalAmount,
        Guid createdBy, Guid? purchaseId = null)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (string.IsNullOrWhiteSpace(supplierRuc))
            throw new ArgumentException("El RUC del proveedor es obligatorio.", nameof(supplierRuc));
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(supplierName));
        if (string.IsNullOrWhiteSpace(accessKey))
            throw new ArgumentException("La clave de acceso es obligatoria.", nameof(accessKey));
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("El número de comprobante es obligatorio.", nameof(invoiceNumber));

        var doc = new PurchaseReceptionDocument
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            CompanyId         = companyId,
            BranchId          = branchId,
            SourceDocType     = sourceDocType,
            SupplierRuc       = supplierRuc.Trim(),
            SupplierName      = supplierName.Trim(),
            SupplierId        = supplierId,
            AccessKey         = accessKey.Trim(),
            InvoiceNumber     = invoiceNumber.Trim(),
            IssueDate         = issueDate,
            AuthorizationDate = authorizationDate,
            Subtotal          = subtotal,
            VatAmount         = vatAmount,
            TotalAmount       = totalAmount,
            Status            = PurchaseReceptionDocumentStatus.Imported,
            PurchaseId        = purchaseId,
        };
        doc.SetCreated(createdBy);
        return doc;
    }

    private void EnsureImported()
    {
        if (Status != PurchaseReceptionDocumentStatus.Imported)
            throw new InvalidOperationException("Solo se puede operar sobre documentos en estado Importado.");
    }

    private void EnsureVerified()
    {
        if (Status != PurchaseReceptionDocumentStatus.Verified)
            throw new InvalidOperationException("Solo se puede operar sobre documentos en estado Verificado.");
    }

    /// <summary>Reservado para la fase de conciliación: marca el documento como verificado contra proveedor/compra.</summary>
    public void MarkVerified(Guid updatedBy)
    {
        EnsureImported();
        Status = PurchaseReceptionDocumentStatus.Verified;
        SetUpdated(updatedBy);
    }

    /// <summary>Reservado para la fase de creación/vínculo de compra — nunca modifica <c>PurchaseInvoice</c>, solo referencia su Id.</summary>
    public void MarkProcessed(Guid purchaseId, Guid updatedBy)
    {
        EnsureVerified();
        if (purchaseId == Guid.Empty)
            throw new ArgumentException("La compra vinculada es obligatoria.", nameof(purchaseId));
        PurchaseId = purchaseId;
        Status = PurchaseReceptionDocumentStatus.Processed;
        SetUpdated(updatedBy);
    }

    public void Cancel(Guid updatedBy)
    {
        if (Status == PurchaseReceptionDocumentStatus.Processed)
            throw new InvalidOperationException("No se puede anular un documento ya procesado.");
        if (Status == PurchaseReceptionDocumentStatus.Cancelled)
            throw new InvalidOperationException("El documento ya está anulado.");
        Status = PurchaseReceptionDocumentStatus.Cancelled;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Fase 3: adjunta el XML autorizado ya consultado en el SRI, persiste sus líneas de detalle
    /// (Item Matching, ya conciliadas por código de proveedor exacto cuando aplica) y transiciona
    /// el documento a Verified. Solo válido desde Imported — si ya está Verified/Processed/Cancelled,
    /// el llamador debe tratarlo como un error de estado, no reintentar la descarga silenciosamente.
    /// </summary>
    public void AttachSriAuthorization(
        string authorizationNumber, DateTime authorizationDate, string xmlContent, DateTime downloadedAtUtc,
        IEnumerable<PurchaseReceptionLine> lines, Guid updatedBy)
    {
        EnsureImported();
        if (string.IsNullOrWhiteSpace(authorizationNumber))
            throw new ArgumentException("El número de autorización es obligatorio.", nameof(authorizationNumber));
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new ArgumentException("El contenido XML no puede estar vacío.", nameof(xmlContent));

        AuthorizationNumber = authorizationNumber.Trim();
        AuthorizationDate   = authorizationDate;
        XmlContent          = xmlContent;
        XmlDownloadedAt     = downloadedAtUtc;
        Status              = PurchaseReceptionDocumentStatus.Verified;
        _lines.Clear();
        _lines.AddRange(lines);
        SetUpdated(updatedBy);
    }
}
