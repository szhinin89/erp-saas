namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Una de las dos contrapartes del comprobante (emisor o receptor). Los campos opcionales
/// solo aplican al emisor en el XML real (<c>obligadoContabilidad</c>, <c>contribuyenteRimpe</c>) —
/// se modelan nullable en vez de crear dos VOs distintos, porque la forma de los datos es la
/// misma y solo cambia qué subconjunto viene poblado según el rol.
/// </summary>
public sealed record RideParty
{
    public string? IdentificationType { get; }
    public string IdentificationNumber { get; }
    public string LegalName { get; }
    public string? TradeName { get; }
    public string? Address { get; }
    public bool? IsAccountingRequired { get; }
    public string? TaxRegime { get; }

    private RideParty(
        string? identificationType,
        string identificationNumber,
        string legalName,
        string? tradeName,
        string? address,
        bool? isAccountingRequired,
        string? taxRegime
    )
    {
        IdentificationType = identificationType;
        IdentificationNumber = identificationNumber;
        LegalName = legalName;
        TradeName = tradeName;
        Address = address;
        IsAccountingRequired = isAccountingRequired;
        TaxRegime = taxRegime;
    }

    public static RideParty Create(
        string? identificationType,
        string identificationNumber,
        string legalName,
        string? tradeName = null,
        string? address = null,
        bool? isAccountingRequired = null,
        string? taxRegime = null
    )
    {
        if (string.IsNullOrWhiteSpace(identificationNumber))
            throw new ArgumentException(
                "La identificación es obligatoria.",
                nameof(identificationNumber)
            );
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("La razón social es obligatoria.", nameof(legalName));

        return new RideParty(
            string.IsNullOrWhiteSpace(identificationType) ? null : identificationType.Trim(),
            identificationNumber.Trim(),
            legalName.Trim(),
            string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            isAccountingRequired,
            string.IsNullOrWhiteSpace(taxRegime) ? null : taxRegime.Trim()
        );
    }
}
