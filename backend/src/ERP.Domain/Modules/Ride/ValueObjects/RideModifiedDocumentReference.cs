namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Referencia al comprobante que un documento modificatorio (Nota de Crédito/Débito) corrige —
/// <c>codDocModificado</c>/<c>numDocModificado</c>/<c>fechaEmisionDocSustento</c> en el XML
/// autorizado. Solo aplica a tipos de comprobante que modifican otro (por eso vive fuera de
/// <see cref="RideHeader"/> como un VO opcional, en vez de campos siempre presentes) — Factura no
/// lo tiene nunca.
///
/// No incluye número de autorización/clave de acceso del comprobante original: el esquema oficial
/// del SRI para Nota de Crédito (<c>NotaCredito_V1.1.0.xsd</c>) no define ningún campo para ese
/// dato — verificado contra el XSD embebido, no es una omisión de este parser. Mismo criterio que
/// <see cref="RideHeader.AuthorizationDate"/>: nunca se inventa un dato que el esquema no provee.
/// </summary>
public sealed record RideModifiedDocumentReference
{
    public string DocumentTypeCode { get; }
    public string Number { get; }
    public DateOnly IssueDate { get; }

    private RideModifiedDocumentReference(string documentTypeCode, string number, DateOnly issueDate)
    {
        DocumentTypeCode = documentTypeCode;
        Number = number;
        IssueDate = issueDate;
    }

    public static RideModifiedDocumentReference Create(
        string documentTypeCode,
        string number,
        DateOnly issueDate
    )
    {
        if (string.IsNullOrWhiteSpace(documentTypeCode))
            throw new ArgumentException(
                "El código de tipo del comprobante modificado es obligatorio.",
                nameof(documentTypeCode)
            );
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException(
                "El número del comprobante modificado es obligatorio.",
                nameof(number)
            );

        return new RideModifiedDocumentReference(documentTypeCode.Trim(), number.Trim(), issueDate);
    }
}
