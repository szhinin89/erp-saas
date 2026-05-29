using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>
/// Perfil fiscal del suscriptor como parte facturada por ZH Technologies.
/// Reemplaza el campo TaxProfileJson de SubscriberBillingAccount con datos estructurados.
/// </summary>
public sealed class SubscriberBillingProfile : AuditableEntity
{
    public const int IdentificationTypeMaxLen  = 20;
    public const int IdentificationNumberMaxLen = 20;
    public const int LegalNameMaxLen            = 200;
    public const int TradeNameMaxLen            = 200;
    public const int AddressMaxLen              = 500;
    public const int PhoneMaxLen                = 30;
    public const int EmailMaxLen                = 200;
    public const int CountryMaxLen              = 3;
    public const int CityMaxLen                 = 100;

    public string  IdentificationType   { get; private set; } = null!;
    public string  IdentificationNumber { get; private set; } = null!;
    public string  LegalName            { get; private set; } = null!;
    public string? TradeName            { get; private set; }
    public string  Address              { get; private set; } = null!;
    public string? Phone                { get; private set; }
    public string? Email                { get; private set; }
    public string  Country              { get; private set; } = "ECU";
    public string? City                 { get; private set; }
    public Guid?   BusinessPartnerId    { get; private set; }

    private SubscriberBillingProfile() { }

    public static SubscriberBillingProfile Create(
        Guid    subscriberId,
        string  identificationType,
        string  identificationNumber,
        string  legalName,
        string  address,
        string  country,
        Guid    createdBy,
        string? tradeName = null,
        string? phone = null,
        string? email = null,
        string? city = null,
        Guid?   businessPartnerId = null)
    {
        var p = new SubscriberBillingProfile
        {
            Id                  = Guid.NewGuid(),
            SubscriberId        = subscriberId,
            IdentificationType  = identificationType.Trim().ToUpperInvariant(),
            IdentificationNumber = identificationNumber.Trim(),
            LegalName           = legalName.Trim(),
            TradeName           = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            Address             = address.Trim(),
            Phone               = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Email               = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Country             = string.IsNullOrWhiteSpace(country) ? "ECU" : country.Trim().ToUpperInvariant(),
            City                = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            BusinessPartnerId   = businessPartnerId,
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void Update(
        string  identificationType,
        string  identificationNumber,
        string  legalName,
        string  address,
        string  country,
        Guid    updatedBy,
        string? tradeName = null,
        string? phone = null,
        string? email = null,
        string? city = null,
        Guid?   businessPartnerId = null)
    {
        IdentificationType  = identificationType.Trim().ToUpperInvariant();
        IdentificationNumber = identificationNumber.Trim();
        LegalName           = legalName.Trim();
        TradeName           = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        Address             = address.Trim();
        Phone               = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email               = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Country             = string.IsNullOrWhiteSpace(country) ? "ECU" : country.Trim().ToUpperInvariant();
        City                = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        BusinessPartnerId   = businessPartnerId;
        SetUpdated(updatedBy);
    }
}
