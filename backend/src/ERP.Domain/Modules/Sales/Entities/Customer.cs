using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.ValueObjects;

namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>Cliente maestro del tenant (persona natural o jurídica). Soft delete vía <see cref="MasterEntity"/>.</summary>
public sealed class Customer : MasterEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid? CompanyId { get; private set; }
    /// <summary>Vínculo al BusinessPartner unificado (null mientras no se migre el registro).</summary>
    public Guid? BusinessPartnerId { get; private set; }
    public const int IdentificationTypeMaxLen = 20;
    public const int IdentificationNumberMaxLen = 32;
    public const int LegalNameMaxLen = 200;
    public const int TradeNameMaxLen = 200;
    public const int AddressLineMaxLen = 500;
    public const int PhoneMaxLen = 40;
    public const int EmailMaxLen = 120;
    public const int NotesMaxLen = 2000;

    /// <summary>RUC, CI, PASSPORT, OTHER (valores estables para API/UI).</summary>
    public string IdentificationType { get; private set; } = null!;
    public string IdentificationNumber { get; private set; } = null!;
    public string LegalName { get; private set; } = null!;
    public string? TradeName { get; private set; }
    public string? AddressLine { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Notes { get; private set; }

    // ── Campos SRI / comerciales ──────────────────────────────────
    /// <summary>ISO-3 del país. FK → sri_country.code. Ej: "ECU".</summary>
    public string?  CountryCode { get; private set; }
    /// <summary>Días de crédito acordados (0 = contado).</summary>
    public short    PaymentDays { get; private set; }
    /// <summary>Límite de crédito aprobado. Null = sin límite.</summary>
    public decimal? CreditLimit { get; private set; }

    /// <summary>
    /// Código SRI del tipo de identificación. No persiste en BD.
    /// Requerido para generar el XML del comprobante electrónico.
    /// </summary>
    public string SriIdTypeCode => IdentificationType switch
    {
        "RUC"      => "04",
        "CI"       => "05",
        "PASSPORT" => "06",
        "OTHER"    => "08",
        _          => "07"
    };

    private Customer() { }

    public static Customer Create(
        Guid subscriberId,
        string identificationType,
        string identificationNumber,
        string legalName,
        string? tradeName,
        string? addressLine,
        string? phone,
        string? email,
        string? notes,
        Guid createdBy,
        string? countryCode  = null,
        short   paymentDays  = 0,
        decimal? creditLimit = null,
        Guid? companyId = null)
    {
        var identification = CustomerIdentification.Create(identificationType, identificationNumber);
        var name = legalName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("La razón social o nombre es obligatoria.", nameof(legalName));
        var validEmail = CustomerEmail.CreateOptional(email);

        var c = new Customer
        {
            Id                   = Guid.NewGuid(),
            SubscriberId             = subscriberId,
            CompanyId            = companyId,
            IdentificationType   = identification.Type,
            IdentificationNumber = identification.Number,
            LegalName            = name,
            TradeName            = NullIfWhiteSpace(tradeName),
            AddressLine          = NullIfWhiteSpace(addressLine),
            Phone                = NullIfWhiteSpace(phone),
            Email                = validEmail?.Value,
            Notes                = NullIfWhiteSpace(notes),
            CountryCode          = NullIfWhiteSpace(countryCode),
            PaymentDays          = paymentDays,
            CreditLimit          = creditLimit,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void Update(
        string identificationType,
        string identificationNumber,
        string legalName,
        string? tradeName,
        string? addressLine,
        string? phone,
        string? email,
        string? notes,
        Guid updatedBy,
        string? countryCode  = null,
        short   paymentDays  = 0,
        decimal? creditLimit = null)
    {
        var identification = CustomerIdentification.Create(identificationType, identificationNumber);
        IdentificationType   = identification.Type;
        IdentificationNumber = identification.Number;
        LegalName = legalName.Trim();
        if (string.IsNullOrWhiteSpace(LegalName))
            throw new ArgumentException("La razón social o nombre es obligatoria.", nameof(legalName));
        TradeName   = NullIfWhiteSpace(tradeName);
        AddressLine = NullIfWhiteSpace(addressLine);
        Phone       = NullIfWhiteSpace(phone);
        Email       = CustomerEmail.CreateOptional(email)?.Value;
        Notes       = NullIfWhiteSpace(notes);
        CountryCode = NullIfWhiteSpace(countryCode);
        PaymentDays = paymentDays;
        CreditLimit = creditLimit;
        SetUpdated(updatedBy);
    }

    public static string NormalizeIdentificationType(string identificationType)
    {
        return CustomerIdentification.NormalizeType(identificationType);
    }

    public static string NormalizeIdentificationNumber(string identificationNumber)
    {
        return CustomerIdentification.NormalizeNumber(identificationNumber);
    }

    public void LinkBusinessPartner(Guid businessPartnerId) => BusinessPartnerId = businessPartnerId;

    private static string? NullIfWhiteSpace(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }
}
