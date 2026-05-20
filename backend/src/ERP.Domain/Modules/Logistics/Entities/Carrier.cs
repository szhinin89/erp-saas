using ERP.Domain.Common;

namespace ERP.Domain.Modules.Logistics.Entities;

/// <summary>Authorized transport carrier for delivery guides (SRI doc type 06).</summary>
public sealed class Carrier : MasterEntity
{
    public const int MaxIdentificationTypeLength   = 20;
    public const int MaxIdentificationNumberLength = 20;
    public const int MaxLegalNameLength            = 200;
    public const int MaxLicensePlateLength         = 20;
    public const int MaxPhoneLength                = 30;
    public const int MaxEmailLength                = 200;

    public string  IdentificationType   { get; private set; } = null!;
    public string  IdentificationNumber { get; private set; } = null!;
    public string  LegalName            { get; private set; } = null!;
    public string  LicensePlate         { get; private set; } = null!;
    public string? Phone                { get; private set; }
    public string? Email                { get; private set; }

    private Carrier() { }

    public static Carrier Create(
        Guid   subscriberId,
        string identificationType,
        string identificationNumber,
        string legalName,
        string licensePlate,
        Guid   createdBy,
        string? phone = null,
        string? email = null)
    {
        var carrier = new Carrier
        {
            Id                  = Guid.NewGuid(),
            SubscriberId            = subscriberId,
            IdentificationType  = identificationType.Trim().ToUpperInvariant(),
            IdentificationNumber = identificationNumber.Trim(),
            LegalName           = legalName.Trim(),
            LicensePlate        = licensePlate.Trim().ToUpperInvariant(),
            Phone               = phone?.Trim() is { Length: > 0 } p ? p : null,
            Email               = email?.Trim().ToLower() is { Length: > 0 } e ? e : null,
        };
        carrier.SetCreated(createdBy);
        return carrier;
    }

    public void Update(
        string identificationType,
        string identificationNumber,
        string legalName,
        string licensePlate,
        Guid   updatedBy,
        string? phone = null,
        string? email = null)
    {
        IdentificationType   = identificationType.Trim().ToUpperInvariant();
        IdentificationNumber = identificationNumber.Trim();
        LegalName            = legalName.Trim();
        LicensePlate         = licensePlate.Trim().ToUpperInvariant();
        Phone                = phone?.Trim() is { Length: > 0 } p ? p : null;
        Email                = email?.Trim().ToLower() is { Length: > 0 } e ? e : null;
        SetUpdated(updatedBy);
    }
}
