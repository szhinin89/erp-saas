namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// Modelo común e independiente de todo documento electrónico SRI. Es el único formato que
/// conoce el motor de ElectronicDocuments — lo produce el proveedor de datos del módulo dueño
/// del documento de origen (Sales, Purchases, Inventory, ...) y lo consumirán las fases
/// posteriores (generación de XML) sin volver a tocar entidades, repositorios ni DbContext
/// de ningún módulo de negocio.
///
/// Contiene únicamente tipos primitivos y records planos — cero entidades EF, cero navegación,
/// cero referencia a SalesInvoice ni a ninguna otra entidad de negocio.
///
/// Auditoría Fase 3 (justificación de por qué el contrato tiene esta forma):
/// - <see cref="Totals"/> es nullable: una Guía de Remisión (<c>ElectronicDocumentType.ShippingGuide</c>)
///   no lleva totales monetarios en el SRI — obligarla a fabricar un total de 0 sería un dato falso.
/// - <see cref="ElectronicDocumentDetailLine.Taxes"/>, <see cref="TaxSummary"/> y <see cref="Payments"/>
///   ya eran listas desde la Fase 2 — una lista vacía representa "no aplica" sin necesitar wrapper nullable.
/// - Se eliminó <c>Phone</c> de <see cref="ElectronicDocumentCounterpartyData"/>: el bloque "receptor" del
///   SRI no incluye teléfono; era un campo especulativo sin respaldo en ningún documento electrónico real.
/// - <see cref="ElectronicDocumentIssuerData.TaxRegime"/> es nullable: solo aplica a contribuyentes RIMPE,
///   y ya viene resuelto por el proveedor como el texto fijo exacto exigido por el SRI — nunca un código.
/// </summary>
public sealed record ElectronicDocumentData(
    ElectronicDocumentEmissionContext Emission,
    ElectronicDocumentIssuerData Issuer,
    ElectronicDocumentCounterpartyData Counterparty,
    IReadOnlyList<ElectronicDocumentDetailLine> Details,
    IReadOnlyList<ElectronicDocumentTaxSummary> TaxSummary,
    ElectronicDocumentTotals? Totals,
    IReadOnlyList<ElectronicDocumentPayment> Payments,
    IReadOnlyList<ElectronicDocumentAdditionalField> AdditionalInfo
);

/// <summary>Establecimiento, punto de emisión, secuencial y ambiente — contexto de emisión SRI.</summary>
public sealed record ElectronicDocumentEmissionContext(
    string Environment,
    string EmissionType,
    /// <summary>Código de tipo de comprobante SRI (tabla <c>sri_doc_types</c>, p.ej. "01" = Factura).
    /// Lo resuelve y valida el proveedor de datos contra el catálogo — el XmlBuilder nunca lo hardcodea.</summary>
    string DocTypeCode,
    string Establishment,
    /// <summary>Dirección del establecimiento emisor (dirEstablecimiento) — puede diferir de la matriz.</summary>
    string EstablishmentAddress,
    string EmissionPoint,
    string Sequential,
    DateTime IssueDate
);

/// <summary>Empresa emisora del comprobante.</summary>
public sealed record ElectronicDocumentIssuerData(
    string TaxId,
    string LegalName,
    string? TradeName,
    /// <summary>Dirección del establecimiento matriz (dirMatriz) — distinta de la del establecimiento emisor.</summary>
    string MatrixAddress,
    /// <summary>Texto fijo "CONTRIBUYENTE RÉGIMEN RIMPE" ya resuelto por el proveedor — nulo para
    /// contribuyentes de régimen general o especial (no aplica). El XmlBuilder solo decide si
    /// incluye el elemento; nunca traduce códigos de régimen.</summary>
    string? TaxRegime,
    bool IsAccountingRequired
);

/// <summary>Cliente/receptor del comprobante — identificación y dirección.</summary>
public sealed record ElectronicDocumentCounterpartyData(
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? Address,
    string? Email
);

/// <summary>Línea de detalle del comprobante con sus impuestos asociados.</summary>
public sealed record ElectronicDocumentDetailLine(
    string Code,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    IReadOnlyList<ElectronicDocumentDetailTax> Taxes
);

public sealed record ElectronicDocumentDetailTax(
    string TaxCode,
    string TaxPercentageCode,
    decimal TaxableBase,
    decimal TaxRate,
    decimal TaxAmount
);

/// <summary>Resumen de impuestos agregado a nivel de documento (no por línea).</summary>
public sealed record ElectronicDocumentTaxSummary(
    string TaxCode,
    string TaxPercentageCode,
    decimal TaxableBase,
    decimal TaxAmount
);

public sealed record ElectronicDocumentTotals(
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    string CurrencyCode
);

/// <summary>Forma de pago SRI — un documento puede declarar más de una.</summary>
public sealed record ElectronicDocumentPayment(
    string PaymentMethodCode,
    decimal Amount,
    int? Term,
    string? TimeUnit
);

/// <summary>Campo adicional libre (información adicional del comprobante SRI).</summary>
public sealed record ElectronicDocumentAdditionalField(string Name, string Value);
