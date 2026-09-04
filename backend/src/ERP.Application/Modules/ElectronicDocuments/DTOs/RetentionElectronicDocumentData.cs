using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A — modelo canónico del Comprobante de Retención SRI,
/// análogo a <see cref="ElectronicDocumentData"/> (Factura/Nota de Crédito) pero con forma propia:
/// el esquema SRI <c>ComprobanteRetencion</c> no tiene detalle comercial (líneas de producto,
/// cantidad/precio unitario) ni totales monetarios de venta — tiene un documento sustento
/// (comprobante del proveedor) y líneas de impuesto retenido (base/porcentaje/valor). Forzar esa
/// forma en <see cref="ElectronicDocumentDetailLine"/> habría sido una adaptación con pérdida —
/// "Quantity"/"UnitPrice" no tienen significado para un rubro de retención. Por eso este es un
/// modelo hermano, no una extensión de <see cref="ElectronicDocumentData"/>.
///
/// Reutiliza sin modificar los bloques de <see cref="ElectronicDocumentData"/> que SÍ son
/// genéricos y aplican igual a cualquier comprobante SRI: <see cref="ElectronicDocumentEmissionContext"/>
/// (ambiente/tipoEmision/codDoc/estab/ptoEmi/dirEstablecimiento/secuencial/fechaEmision),
/// <see cref="ElectronicDocumentIssuerData"/> (empresa emisora), <see cref="ElectronicDocumentCounterpartyData"/>
/// (aquí como sujeto retenido) y <see cref="ElectronicDocumentAdditionalField"/> (infoAdicional).
///
/// Se construye ÚNICAMENTE desde <c>RetentionDocument</c> ya <c>Issued</c> y sus snapshots
/// (<c>SourceDocumentSnapshot</c>, líneas, totales ya calculados) — nunca recalcula bases,
/// porcentajes ni montos, y nunca vuelve a consultar el documento de gasto/compra origen para
/// datos que el snapshot ya congeló. Solo consulta Company/Establishment/EmissionPoint/SriSettings/
/// BusinessPartner y catálogos normativos SRI para los datos que <c>RetentionDocument</c> no
/// snapshotea (identidad de la empresa emisora, dirección de establecimientos, sujeto retenido).
///
/// Cero entidades EF, cero navegación — solo primitivos y records planos, mismo criterio que
/// <see cref="ElectronicDocumentData"/>.
/// </summary>
public sealed record RetentionElectronicDocumentData(
    RetentionElectronicDocumentMetadata Metadata,
    ElectronicDocumentEmissionContext Emission,
    /// <summary>Número completo "estab-ptoEmi-secuencial" — copiado tal cual de
    /// <c>RetentionDocument.RetentionNumber</c> (ya generado por <c>CaptureNextAsync(..., "07")</c>
    /// en RETENTIONS-DOCUMENT-SEQUENCE-02E). Nunca se reconstruye ni se vuelve a capturar aquí.</summary>
    string NumeroCompleto,
    ElectronicDocumentIssuerData Issuer,
    RetentionElectronicDocumentInfo RetentionInfo,
    /// <summary>Sujeto retenido (proveedor) — mismo record que "Counterparty" en Factura/NC;
    /// aquí identifica a quien se le retuvo, no al comprador.</summary>
    ElectronicDocumentCounterpartyData SubjectWithheld,
    RetentionElectronicDocumentSourceDocument SourceDocument,
    IReadOnlyList<RetentionElectronicDocumentTaxLine> Lines,
    RetentionElectronicDocumentTotals Totals,
    /// <summary>
    /// infoAdicional — siempre vacía en esta fase: <c>RetentionDocument</c> no tiene un campo de
    /// notas/observación equivalente a <c>SalesInvoice.Notes</c> (que es lo único que
    /// <see cref="ERP.Application.Modules.Sales.Services.SalesInvoiceElectronicDocumentDataProvider"/>
    /// vierte aquí). El mecanismo real (lista, nunca null) ya existe y queda listo si una fase
    /// futura agrega ese campo — no se fuerza una tabla ni un campo nuevo en esta fase.
    /// </summary>
    IReadOnlyList<ElectronicDocumentAdditionalField> AdditionalInfo,
    /// <summary>
    /// claveAcceso — SIEMPRE <c>null</c> en esta fase, deliberadamente. Mismo patrón exacto que
    /// Factura/Nota de Crédito: el AccessKey nunca lo calcula el proveedor de datos — lo calcula
    /// el XmlBuilder correspondiente (<c>InvoiceXmlBuilder</c>/<c>CreditNoteXmlBuilder</c> hoy;
    /// el futuro <c>RetentionXmlBuilder</c> en RETENTIONS-SRI-XML-MAPPER-03B) a partir de
    /// <see cref="Emission"/>/<see cref="Issuer"/>/<see cref="NumeroCompleto"/>. Se expone aquí ya
    /// como campo preparatorio (nullable) para que el mapper futuro no necesite tocar este modelo.
    /// </summary>
    string? AccessKey = null
);

/// <summary>
/// Metadatos internos de trazabilidad — nunca forman parte del XML SRI, sirven para auditoría/
/// almacenamiento documental y para que el mapper/RIDE futuros puedan re-vincular este modelo al
/// agregado origen sin volver a resolverlo desde cero.
/// </summary>
public sealed record RetentionElectronicDocumentMetadata(
    Guid RetentionId,
    Guid TenantId,
    Guid CompanyId,
    Guid EmissionPointId,
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    /// <summary>Momento en que se construyó ESTE modelo (no la fecha de emisión de la retención) — para auditoría de cuándo se generó el snapshot electrónico.</summary>
    DateTime GeneratedAtUtc
);

/// <summary>
/// Bloque "infoCompRetencion" que no se resuelve reutilizando otro record ya genérico:
/// <c>fechaEmision</c>/<c>dirEstablecimiento</c>/<c>obligadoContabilidad</c> ya vienen de
/// <see cref="ElectronicDocumentEmissionContext"/>/<see cref="ElectronicDocumentIssuerData"/>, y
/// <c>tipoIdentificacionSujetoRetenido</c>/<c>razonSocialSujetoRetenido</c>/<c>identificacionSujetoRetenido</c>
/// vienen de <see cref="ElectronicDocumentCounterpartyData"/> (como <c>SubjectWithheld</c>).
/// </summary>
public sealed record RetentionElectronicDocumentInfo(
    /// <summary>contribuyenteEspecial — número de resolución si la empresa es contribuyente especial, <c>null</c> si no aplica (el elemento SRI se omite). Copiado tal cual de <c>Company.SpecialTaxpayerNo</c>.</summary>
    string? SpecialTaxpayerNumber,
    /// <summary>periodoFiscal, formato SRI "mm/aaaa" — copiado tal cual de <c>RetentionDocument.FiscalPeriod</c> (ya derivado por el agregado al emitir, nunca recalculado aquí).</summary>
    string FiscalPeriod
);

/// <summary>
/// Documento sustento (comprobante del proveedor que originó la retención) — copiado 1:1 de
/// <c>RetentionDocument.SourceDocument*</c> (snapshot congelado por <c>RetentionIssuer</c> al
/// emitir, ver RETENTIONS-TAX-COMPONENT-MODEL-02B/RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G).
/// Ningún campo se recalcula ni se vuelve a consultar contra el documento de gasto/compra origen
/// — ese documento puede haber cambiado o dejado de existir desde que se emitió la retención.
/// </summary>
public sealed record RetentionElectronicDocumentSourceDocument(
    /// <summary>codSustento (01-19) — puede ser <c>null</c> si el gasto origen no tenía código propio ni default de proveedor configurado (gap conocido y aceptado, ver RETENTIONS-SOURCE-DOCUMENT-TAX-SUPPORT-02G). Nunca se sustituye por un valor inventado aquí.</summary>
    string? TaxSupportCode,
    /// <summary>codDocSustento — código SRI de tipo de documento del comprobante sustento (p.ej. "01" Factura, "03" Liquidación de compra).</summary>
    string? DocTypeCode,
    /// <summary>numDocSustento — número completo del comprobante sustento.</summary>
    string? Number,
    /// <summary>numAutDocSustento — número de autorización SRI del comprobante sustento, si existe.</summary>
    string? AuthorizationNumber,
    DateOnly? IssueDate,
    /// <summary>totalSinImpuestos.</summary>
    decimal? Subtotal,
    /// <summary>importeTotal.</summary>
    decimal? Total
);

/// <summary>
/// Una línea de <c>&lt;impuesto&gt;</c> del comprobante de retención — copiada 1:1 de una
/// <c>RetentionDocumentLine</c> ya persistida. <see cref="BaseAmount"/>/<see cref="RetentionRate"/>/
/// <see cref="RetainedAmount"/> son los valores ya calculados y redondeados por el agregado en el
/// momento de emitir — nunca se recalculan aquí.
/// </summary>
public sealed record RetentionElectronicDocumentTaxLine(
    RetentionTaxType TaxType,
    /// <summary>Código SRI de tipo de impuesto retenido (Tabla 21: "1"=Renta, "2"=IVA) — resuelto desde <c>SriRetentionTaxTypeCodes</c> según <see cref="TaxType"/>, nunca un literal en el proveedor.</summary>
    string SriTaxTypeCode,
    /// <summary>codigoRetencion — el código de retención propiamente dicho (p.ej. "303", "725"), distinto del código de tipo de impuesto.</summary>
    string RetentionCode,
    /// <summary>descripcionRetencion — snapshot congelado del nombre del código al emitir (<c>RetentionDocumentLine.RetentionCodeDescription</c>), nunca resuelto de nuevo contra el catálogo.</summary>
    string RetentionCodeDescription,
    /// <summary>baseImponible.</summary>
    decimal BaseAmount,
    /// <summary>porcentajeRetener.</summary>
    decimal RetentionRate,
    /// <summary>valorRetenido.</summary>
    decimal RetainedAmount
);

/// <summary>
/// Totales derivados — copiados 1:1 de <c>RetentionDocument.TotalRetainedVat/Income/Retained</c>,
/// que el propio agregado ya recalcula desde sus líneas cada vez que cambian
/// (<c>RecalculateTotals()</c>). Este record nunca vuelve a sumar <see cref="RetentionElectronicDocumentTaxLine"/>
/// por su cuenta — sería una segunda fuente de verdad para el mismo número.
/// </summary>
public sealed record RetentionElectronicDocumentTotals(
    decimal TotalRetainedVat,
    decimal TotalRetainedIncome,
    decimal TotalRetained
);
