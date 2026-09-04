namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — datos identificadores del Comprobante de Retención tal como
/// aparecen en <c>infoTributaria</c>/<c>infoCompRetencion</c> del XML autorizado.
///
/// No reutiliza <see cref="RideHeader"/>: ese VO modela <c>CurrencyCode</c>/<c>TotalDiscount</c>/
/// <c>Tip</c>/<c>GrandTotal</c>, conceptos que <c>comprobanteRetencion</c> no tiene (no es un
/// documento de venta con totales monetarios) — forzarlos habría significado inventar una moneda
/// o un total que el esquema SRI de retención no define. <see cref="FiscalPeriod"/> es propio de
/// este comprobante (no existe en Factura/Nota de Crédito).
///
/// <see cref="AuthorizationNumber"/> se deriva de <see cref="AccessKey"/> (regla AUTH-01, mismo
/// criterio ya verificado para <c>RideHeader</c> en Invoice/CreditNote: el número de autorización
/// del SRI ES la clave de acceso). <see cref="AuthorizationDate"/> es <see langword="null"/>-able
/// a propósito — viaja en el envoltorio de autorización, no dentro de <c>comprobanteRetencion</c>
/// en sí; nunca se inventa si el XML todavía no está autorizado.
/// </summary>
public sealed record RetentionRideHeader
{
    public string Environment { get; }
    public string EmissionType { get; }
    public string DocumentTypeCode { get; }
    public string Establishment { get; }
    public string EmissionPoint { get; }
    public string Sequential { get; }
    public string EstablishmentAddress { get; }
    public DateOnly IssueDate { get; }
    public string FiscalPeriod { get; }
    public RideAccessKey AccessKey { get; }
    public string AuthorizationNumber { get; }
    public DateTime? AuthorizationDate { get; }

    private RetentionRideHeader(
        string environment,
        string emissionType,
        string documentTypeCode,
        string establishment,
        string emissionPoint,
        string sequential,
        string establishmentAddress,
        DateOnly issueDate,
        string fiscalPeriod,
        RideAccessKey accessKey,
        string authorizationNumber,
        DateTime? authorizationDate
    )
    {
        Environment = environment;
        EmissionType = emissionType;
        DocumentTypeCode = documentTypeCode;
        Establishment = establishment;
        EmissionPoint = emissionPoint;
        Sequential = sequential;
        EstablishmentAddress = establishmentAddress;
        IssueDate = issueDate;
        FiscalPeriod = fiscalPeriod;
        AccessKey = accessKey;
        AuthorizationNumber = authorizationNumber;
        AuthorizationDate = authorizationDate;
    }

    public static RetentionRideHeader Create(
        string environment,
        string emissionType,
        string documentTypeCode,
        string establishment,
        string emissionPoint,
        string sequential,
        string establishmentAddress,
        DateOnly issueDate,
        string fiscalPeriod,
        RideAccessKey accessKey,
        string authorizationNumber,
        DateTime? authorizationDate
    )
    {
        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("El ambiente SRI es obligatorio.", nameof(environment));
        if (string.IsNullOrWhiteSpace(emissionType))
            throw new ArgumentException(
                "El tipo de emisión SRI es obligatorio.",
                nameof(emissionType)
            );
        if (string.IsNullOrWhiteSpace(documentTypeCode))
            throw new ArgumentException(
                "El código de tipo de comprobante es obligatorio.",
                nameof(documentTypeCode)
            );
        if (establishment is not { Length: 3 } || !establishment.All(char.IsDigit))
            throw new ArgumentException(
                "El código de establecimiento debe tener 3 dígitos.",
                nameof(establishment)
            );
        if (emissionPoint is not { Length: 3 } || !emissionPoint.All(char.IsDigit))
            throw new ArgumentException(
                "El código de punto de emisión debe tener 3 dígitos.",
                nameof(emissionPoint)
            );
        if (sequential is not { Length: 9 } || !sequential.All(char.IsDigit))
            throw new ArgumentException("El secuencial debe tener 9 dígitos.", nameof(sequential));
        if (string.IsNullOrWhiteSpace(establishmentAddress))
            throw new ArgumentException(
                "La dirección del establecimiento es obligatoria.",
                nameof(establishmentAddress)
            );
        if (string.IsNullOrWhiteSpace(fiscalPeriod))
            throw new ArgumentException("El período fiscal es obligatorio.", nameof(fiscalPeriod));
        ArgumentNullException.ThrowIfNull(accessKey);
        if (string.IsNullOrWhiteSpace(authorizationNumber))
            throw new ArgumentException(
                "El número de autorización es obligatorio.",
                nameof(authorizationNumber)
            );

        return new RetentionRideHeader(
            environment.Trim(),
            emissionType.Trim(),
            documentTypeCode.Trim(),
            establishment,
            emissionPoint,
            sequential,
            establishmentAddress.Trim(),
            issueDate,
            fiscalPeriod.Trim(),
            accessKey,
            authorizationNumber.Trim(),
            authorizationDate
        );
    }
}
