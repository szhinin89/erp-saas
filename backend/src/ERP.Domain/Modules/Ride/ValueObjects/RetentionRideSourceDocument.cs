namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — documento sustento (comprobante del proveedor) tal como
/// aparece en cada <c>&lt;impuesto&gt;</c> del XML autorizado de retención (<c>codDocSustento</c>/
/// <c>numDocSustento</c>/<c>fechaEmisionDocSustento</c>).
///
/// Los tres campos son opcionales porque el esquema SRI <c>ComprobanteRetencion_V1.0.0.xsd</c> los
/// declara <c>minOccurs="0"</c> — no todo comprobante los incluye (ver <c>RetentionXmlBuilder</c>,
/// que los omite si el dato de origen no cumple el patrón exigido). Ese mismo XSD no define
/// <c>codSustento</c> ni totales del documento sustento (<c>totalSinImpuestos</c>/<c>importeTotal</c>)
/// — ausentes aquí porque no existen en el XML, no porque este VO los recorte.
/// </summary>
public sealed record RetentionRideSourceDocument
{
    public string? DocumentTypeCode { get; }
    public string? Number { get; }
    public DateOnly? IssueDate { get; }

    private RetentionRideSourceDocument(
        string? documentTypeCode,
        string? number,
        DateOnly? issueDate
    )
    {
        DocumentTypeCode = documentTypeCode;
        Number = number;
        IssueDate = issueDate;
    }

    public static RetentionRideSourceDocument Create(
        string? documentTypeCode,
        string? number,
        DateOnly? issueDate
    ) =>
        new(
            string.IsNullOrWhiteSpace(documentTypeCode) ? null : documentTypeCode.Trim(),
            string.IsNullOrWhiteSpace(number) ? null : number.Trim(),
            issueDate
        );

    /// <summary>Los tres campos ausentes del XML — estado válido (XSD 1.0.0), no un error.</summary>
    public static RetentionRideSourceDocument Empty() => new(null, null, null);
}
