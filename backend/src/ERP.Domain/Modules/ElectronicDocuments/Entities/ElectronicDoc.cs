using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Cabecera común de TODOS los comprobantes que emite la empresa.
/// Extensiones 1:1: SalesInvoice, CreditNote, DebitNote, DeliveryGuide,
/// WithholdingCertificate, PurchaseSettlement.
/// Estado: draft → validated → authorized | rejected | error | voided
/// </summary>
public class ElectronicDoc
{
    public Guid    Id                  { get; set; }
    public Guid    CompanyId           { get; set; }
    public Guid    EmissionPointId     { get; set; }
    public string  DocTypeCode         { get; set; } = null!;
    // Numeración SRI: establecimiento-punto_emision-secuencial
    public string  EstablishmentCode   { get; set; } = null!;
    public string  EmissionPointCode   { get; set; } = null!;
    public string  Sequential          { get; set; } = null!;
    // Clave de acceso (49 dígitos, generada por la app antes de firmar el XML)
    public string? AccessKey           { get; set; }
    // Fechas
    public DateOnly  IssueDate         { get; set; }
    public DateTime? AuthDate          { get; set; }
    public string?   AuthNumber        { get; set; }
    // Estado
    public string    Status            { get; set; } = "draft";
    // XML
    public string? XmlSignedPath       { get; set; }
    public string? XmlAuthPath         { get; set; }
    public string? ErrorCode           { get; set; }
    public string? ErrorMessage        { get; set; }
    // Totales SRI (totalConImpuestos)
    public decimal SubtotalVat0        { get; set; }
    public decimal SubtotalTaxable     { get; set; }
    public decimal SubtotalExempt      { get; set; }
    public decimal SubtotalNoObject    { get; set; }
    public decimal SubtotalNoVatObj    { get; set; }
    public decimal TotalDiscount       { get; set; }
    public decimal TotalVat            { get; set; }
    public decimal TotalIce            { get; set; }
    public decimal TotalOtherTaxes     { get; set; }
    public decimal Total               { get; set; }
    /// <summary>
    /// JSONB: informacionAdicional del XML (pares clave-valor libres).
    /// Usar JsonSerializer.Deserialize&lt;Dictionary&lt;string,string&gt;&gt;() para parsear.
    /// </summary>
    public string? AdditionalInfo      { get; set; }
    public Guid?   JournalEntryId      { get; set; }
    public string? Notes               { get; set; }
    public DateTime CreatedAt          { get; set; }
    public DateTime UpdatedAt          { get; set; }
    public Guid?   CreatedBy           { get; set; }
    public Guid?   UpdatedBy           { get; set; }

    // Navigation
    public EmissionPoint EmissionPoint { get; set; } = null!;
    public SriDocType    DocType       { get; set; } = null!;
    public SriErrorCode? Error         { get; set; }

    // Extensiones 1:1 (solo una estará presente según DocTypeCode)
    public SalesInvoice?      SalesInvoice      { get; set; }
    public CreditNote?        CreditNote        { get; set; }
    public DebitNote?         DebitNote         { get; set; }
    public DeliveryGuide?     DeliveryGuide     { get; set; }
    public WithholdingCertificate? WithholdingCertificate { get; set; }
    public PurchaseSettlement? PurchaseSettlement { get; set; }

    // Líneas
    public ICollection<InvoiceDetail>     InvoiceLines  { get; set; } = [];
    public ICollection<NoteDetail>        NoteLines     { get; set; } = [];
    public ICollection<DeliveryDetail>    DeliveryLines { get; set; } = [];
    public ICollection<WithholdingDetail> WhLines       { get; set; } = [];
    public ICollection<DocPayment>        Payments      { get; set; } = [];
    public ICollection<DocTax>            Taxes         { get; set; } = [];
}
