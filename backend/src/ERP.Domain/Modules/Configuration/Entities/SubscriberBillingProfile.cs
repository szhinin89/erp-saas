using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// MODELO CANÓNICO UNIFICADO — Perfil de facturación del suscriptor.
/// Reemplaza a BillingSettings (datos ERP local) y SubscriberBillingProfile (datos SaaS).
/// Contiene: identidad fiscal SRI + configuración de recibos + datos de contacto.
/// Una sola fila por subscriber (índice único en subscriber_id).
/// </summary>
public sealed class SubscriberBillingProfile : AuditableEntity, ISubscriberScopedEntity
{
    public const int IdentificationTypeMaxLen   = 5;   // SRI code: "04"
    public const int IdentificationNumberMaxLen = 20;  // RUC 13 dígitos
    public const int LegalNameMaxLen            = 200;
    public const int TradeNameMaxLen            = 200;
    public const int AddressMaxLen              = 500;
    public const int PhoneMaxLen                = 50;
    public const int EmailMaxLen                = 200;
    public const int CountryMaxLen              = 3;   // ISO-3 "ECU"
    public const int CityMaxLen                 = 100;
    public const int SpecialTaxpayerMaxLen      = 200;
    public const int LogoBase64MaxLen           = 10000;
    public const int FooterTextMaxLen           = 500;

    // ── Identificación fiscal SRI ─────────────────────────────────
    /// <summary>Código SRI de tipo de identificación (global.sri_id_type.Code). "04" = RUC.</summary>
    public string  IdentificationType   { get; private set; } = "04";
    /// <summary>Número de identificación (RUC para Ecuador).</summary>
    public string  IdentificationNumber { get; private set; } = null!;

    // ── Datos legales ─────────────────────────────────────────────
    public string  LegalName            { get; private set; } = null!;
    public string? TradeName            { get; private set; }
    public string  Address              { get; private set; } = null!;
    public string? Phone                { get; private set; }
    public string? Email                { get; private set; }
    public string  Country              { get; private set; } = "ECU";
    public string? City                 { get; private set; }

    // ── Régimen fiscal SRI ────────────────────────────────────────
    public bool    RequiresAccounting   { get; private set; }
    /// <summary>Número de resolución de Contribuyente Especial (null si no aplica).</summary>
    public string? SpecialTaxpayer      { get; private set; }

    // ── Configuración de recibos e impresión ─────────────────────
    public string? LogoBase64           { get; private set; }
    public string? FooterText           { get; private set; }
    public int     ReceiptWidth         { get; private set; } = 80;

    // ── Vínculo con BusinessPartner (para facturación SaaS) ──────
    public Guid?   BusinessPartnerId    { get; private set; }

    private SubscriberBillingProfile() { }

    public static SubscriberBillingProfile Create(
        Guid    subscriberId,
        string  identificationType,
        string  identificationNumber,
        string  legalName,
        string  address,
        Guid    createdBy,
        string? tradeName           = null,
        string? phone               = null,
        string? email               = null,
        string  country             = "ECU",
        string? city                = null,
        bool    requiresAccounting  = false,
        string? specialTaxpayer     = null,
        string? logoBase64          = null,
        string? footerText          = null,
        int     receiptWidth        = 80,
        Guid?   businessPartnerId   = null)
    {
        var p = new SubscriberBillingProfile
        {
            Id                   = Guid.NewGuid(),
            SubscriberId         = subscriberId,
            IdentificationType   = identificationType.Trim(),
            IdentificationNumber = identificationNumber.Trim(),
            LegalName            = legalName.Trim(),
            TradeName            = string.IsNullOrWhiteSpace(tradeName)     ? null : tradeName.Trim(),
            Address              = address.Trim(),
            Phone                = string.IsNullOrWhiteSpace(phone)         ? null : phone.Trim(),
            Email                = string.IsNullOrWhiteSpace(email)         ? null : email.Trim(),
            Country              = string.IsNullOrWhiteSpace(country)       ? "ECU" : country.Trim().ToUpperInvariant(),
            City                 = string.IsNullOrWhiteSpace(city)          ? null : city.Trim(),
            RequiresAccounting   = requiresAccounting,
            SpecialTaxpayer      = string.IsNullOrWhiteSpace(specialTaxpayer) ? null : specialTaxpayer.Trim(),
            LogoBase64           = string.IsNullOrWhiteSpace(logoBase64)    ? null : logoBase64.Trim(),
            FooterText           = string.IsNullOrWhiteSpace(footerText)    ? null : footerText.Trim(),
            ReceiptWidth         = receiptWidth == 58 ? 58 : 80,
            BusinessPartnerId    = businessPartnerId,
        };
        p.SetCreated(createdBy);
        return p;
    }

    /// <summary>Crea perfil por defecto para nuevos subscribers (usar hasta que configuren sus datos).</summary>
    public static SubscriberBillingProfile CreateDefault(Guid subscriberId, Guid createdBy)
        => Create(subscriberId, "04", "9999999999999", "EMPRESA DEMO", "Sin dirección", createdBy,
                  footerText: "Gracias por su preferencia.");

    public void Update(
        string  identificationType,
        string  identificationNumber,
        string  legalName,
        string  address,
        Guid    updatedBy,
        string? tradeName           = null,
        string? phone               = null,
        string? email               = null,
        string  country             = "ECU",
        string? city                = null,
        bool    requiresAccounting  = false,
        string? specialTaxpayer     = null,
        string? logoBase64          = null,
        string? footerText          = null,
        int     receiptWidth        = 80,
        Guid?   businessPartnerId   = null)
    {
        IdentificationType   = identificationType.Trim();
        IdentificationNumber = identificationNumber.Trim();
        LegalName            = legalName.Trim();
        TradeName            = string.IsNullOrWhiteSpace(tradeName)       ? null : tradeName.Trim();
        Address              = address.Trim();
        Phone                = string.IsNullOrWhiteSpace(phone)           ? null : phone.Trim();
        Email                = string.IsNullOrWhiteSpace(email)           ? null : email.Trim();
        Country              = string.IsNullOrWhiteSpace(country)         ? "ECU" : country.Trim().ToUpperInvariant();
        City                 = string.IsNullOrWhiteSpace(city)            ? null : city.Trim();
        RequiresAccounting   = requiresAccounting;
        SpecialTaxpayer      = string.IsNullOrWhiteSpace(specialTaxpayer) ? null : specialTaxpayer.Trim();
        LogoBase64           = string.IsNullOrWhiteSpace(logoBase64)      ? null : logoBase64.Trim();
        FooterText           = string.IsNullOrWhiteSpace(footerText)      ? null : footerText.Trim();
        ReceiptWidth         = receiptWidth == 58 ? 58 : 80;
        BusinessPartnerId    = businessPartnerId;
        SetUpdated(updatedBy);
    }
}
