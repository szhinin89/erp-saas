using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class Supplier : MasterEntity, ISubscriberScopedEntity
{
    public const string TypeNatural  = "Natural";
    public const string TypeLegal    = "Legal";

    public const int LegalNameMaxLen    = 200;
    public const int RucMaxLen          = 13;
    public const int EmailMaxLen        = 120;
    public const int PhoneMaxLen        = 40;
    public const int AddressMaxLen      = 300;
    public const int PaymentTermsMaxLen = 30;

    /// <summary>Vínculo al BusinessPartner unificado (null mientras no se migre el registro).</summary>
    public Guid?   BusinessPartnerId { get; private set; }
    public string  PersonType      { get; private set; } = null!;
    public string  LegalName       { get; private set; } = null!;
    public string  Ruc             { get; private set; } = null!;
    public string? Email           { get; private set; }
    public string? Phone           { get; private set; }
    public string? Address         { get; private set; }
    public string  PaymentTerms    { get; private set; } = null!;
    public string?  CountryCode    { get; private set; }
    public string?  TaxSupportCode { get; private set; }
    public short    PaymentDays    { get; private set; }
    public decimal? CreditLimit    { get; private set; }

    public string SriIdTypeCode => PersonType == TypeNatural ? "05" : "04";

    private Supplier() { }

    public static Supplier Create(
        Guid    subscriberId,
        string  personType,
        string  legalName,
        string  ruc,
        string? email,
        string? phone,
        string? address,
        string  paymentTerms,
        Guid    createdBy,
        string? countryCode    = null,
        string? taxSupportCode = null,
        short   paymentDays    = 0,
        decimal? creditLimit   = null)
    {
        ValidateType(personType);
        ValidateRuc(ruc);

        var s = new Supplier
        {
            Id             = Guid.NewGuid(),
            SubscriberId       = subscriberId,
            PersonType     = personType,
            LegalName      = legalName.Trim(),
            Ruc            = ruc.Trim(),
            Email          = Trim(email),
            Phone          = Trim(phone),
            Address        = Trim(address),
            PaymentTerms   = paymentTerms,
            CountryCode    = Trim(countryCode),
            TaxSupportCode = Trim(taxSupportCode),
            PaymentDays    = paymentDays,
            CreditLimit    = creditLimit,
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void Update(
        string  personType,
        string  legalName,
        string  ruc,
        string? email,
        string? phone,
        string? address,
        string  paymentTerms,
        Guid    updatedBy,
        string? countryCode    = null,
        string? taxSupportCode = null,
        short   paymentDays    = 0,
        decimal? creditLimit   = null)
    {
        ValidateType(personType);
        ValidateRuc(ruc);

        PersonType     = personType;
        LegalName      = legalName.Trim();
        Ruc            = ruc.Trim();
        Email          = Trim(email);
        Phone          = Trim(phone);
        Address        = Trim(address);
        PaymentTerms   = paymentTerms;
        CountryCode    = Trim(countryCode);
        TaxSupportCode = Trim(taxSupportCode);
        PaymentDays    = paymentDays;
        CreditLimit    = creditLimit;
        SetUpdated(updatedBy);
    }

    public void LinkBusinessPartner(Guid businessPartnerId) => BusinessPartnerId = businessPartnerId;

    private static void ValidateType(string type)
    {
        if (type != TypeNatural && type != TypeLegal)
            throw new ArgumentException($"PersonType must be '{TypeNatural}' or '{TypeLegal}'.", nameof(type));
    }

    private static void ValidateRuc(string ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc) || ruc.Trim().Length != 13)
            throw new ArgumentException("RUC must be exactly 13 digits.", nameof(ruc));
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
