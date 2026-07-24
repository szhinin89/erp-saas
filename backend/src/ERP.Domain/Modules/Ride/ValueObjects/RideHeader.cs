namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Datos identificadores y de autorización del comprobante, tal como aparecen en
/// <c>infoTributaria</c>/<c>infoFactura</c> del XML autorizado (ficha técnica SRI).
///
/// <see cref="AuthorizationNumber"/> es obligatorio pero nunca se lee de un campo propio del
/// XML: en el esquema offline el número de autorización ES, por regla del SRI (AUTH-01, ya
/// verificado en ElectronicDocuments), idéntico a la clave de acceso — el parser lo deriva de
/// <see cref="AccessKey"/>, presente en <c>infoTributaria</c>.
///
/// <see cref="AuthorizationDate"/> es <see langword="null"/>-able a propósito: el XML que
/// ElectronicDocuments persiste como "autorizado" (<c>SriAutorizacionResult.DocumentXml</c>,
/// ver <c>SriSoapClient.ParseAutorizacionResponse</c>) es únicamente el contenido de
/// <c>&lt;comprobante&gt;</c> — la fecha de autorización viaja en un campo hermano
/// (<c>fechaAutorizacion</c>) que nunca se persiste dentro del XML en sí. Corrección de diseño
/// respecto a la versión original de esta fase (Fase 2), hecha en la Fase 6 al construir el
/// primer parser real contra el XML real — nunca inventar este dato si no está disponible.
/// </summary>
public sealed record RideHeader
{
    public string Environment { get; }
    public string EmissionType { get; }
    public string DocumentTypeCode { get; }
    public string Establishment { get; }
    public string EmissionPoint { get; }
    public string Sequential { get; }
    public string EstablishmentAddress { get; }
    public DateOnly IssueDate { get; }
    public string CurrencyCode { get; }
    public RideAccessKey AccessKey { get; }
    public string AuthorizationNumber { get; }
    public DateTime? AuthorizationDate { get; }
    public decimal SubtotalWithoutTax { get; }
    public decimal TotalDiscount { get; }
    public decimal Tip { get; }
    public decimal GrandTotal { get; }

    private RideHeader(
        string environment, string emissionType, string documentTypeCode,
        string establishment, string emissionPoint, string sequential, string establishmentAddress,
        DateOnly issueDate, string currencyCode, RideAccessKey accessKey,
        string authorizationNumber, DateTime? authorizationDate,
        decimal subtotalWithoutTax, decimal totalDiscount, decimal tip, decimal grandTotal)
    {
        Environment = environment;
        EmissionType = emissionType;
        DocumentTypeCode = documentTypeCode;
        Establishment = establishment;
        EmissionPoint = emissionPoint;
        Sequential = sequential;
        EstablishmentAddress = establishmentAddress;
        IssueDate = issueDate;
        CurrencyCode = currencyCode;
        AccessKey = accessKey;
        AuthorizationNumber = authorizationNumber;
        AuthorizationDate = authorizationDate;
        SubtotalWithoutTax = subtotalWithoutTax;
        TotalDiscount = totalDiscount;
        Tip = tip;
        GrandTotal = grandTotal;
    }

    public static RideHeader Create(
        string environment, string emissionType, string documentTypeCode,
        string establishment, string emissionPoint, string sequential, string establishmentAddress,
        DateOnly issueDate, string currencyCode, RideAccessKey accessKey,
        string authorizationNumber, DateTime? authorizationDate,
        decimal subtotalWithoutTax, decimal totalDiscount, decimal tip, decimal grandTotal)
    {
        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("El ambiente SRI es obligatorio.", nameof(environment));
        if (string.IsNullOrWhiteSpace(emissionType))
            throw new ArgumentException("El tipo de emisión SRI es obligatorio.", nameof(emissionType));
        if (string.IsNullOrWhiteSpace(documentTypeCode))
            throw new ArgumentException("El código de tipo de comprobante es obligatorio.", nameof(documentTypeCode));
        if (establishment is not { Length: 3 } || !establishment.All(char.IsDigit))
            throw new ArgumentException("El código de establecimiento debe tener 3 dígitos.", nameof(establishment));
        if (emissionPoint is not { Length: 3 } || !emissionPoint.All(char.IsDigit))
            throw new ArgumentException("El código de punto de emisión debe tener 3 dígitos.", nameof(emissionPoint));
        if (sequential is not { Length: 9 } || !sequential.All(char.IsDigit))
            throw new ArgumentException("El secuencial debe tener 9 dígitos.", nameof(sequential));
        if (string.IsNullOrWhiteSpace(establishmentAddress))
            throw new ArgumentException("La dirección del establecimiento es obligatoria.", nameof(establishmentAddress));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("La moneda es obligatoria.", nameof(currencyCode));
        ArgumentNullException.ThrowIfNull(accessKey);
        if (string.IsNullOrWhiteSpace(authorizationNumber))
            throw new ArgumentException("El número de autorización es obligatorio.", nameof(authorizationNumber));
        if (subtotalWithoutTax < 0)
            throw new ArgumentException("El subtotal no puede ser negativo.", nameof(subtotalWithoutTax));
        if (totalDiscount < 0)
            throw new ArgumentException("El descuento total no puede ser negativo.", nameof(totalDiscount));
        if (tip < 0)
            throw new ArgumentException("La propina no puede ser negativa.", nameof(tip));
        if (grandTotal < 0)
            throw new ArgumentException("El total no puede ser negativo.", nameof(grandTotal));

        return new RideHeader(
            environment.Trim(), emissionType.Trim(), documentTypeCode.Trim(),
            establishment, emissionPoint, sequential, establishmentAddress.Trim(),
            issueDate, currencyCode.Trim(), accessKey,
            authorizationNumber.Trim(), authorizationDate,
            subtotalWithoutTax, totalDiscount, tip, grandTotal);
    }
}
