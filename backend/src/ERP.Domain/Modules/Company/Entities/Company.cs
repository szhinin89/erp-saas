using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Empresa emisora de comprobantes electrónicos (RUC).
/// Un <see cref="Subscriber"/> puede tener N companies (holdings / franquicias).
/// Tablas SRI y documentos electrónicos referencian <c>company_id</c>.
/// </summary>
public class Company
{
    public Guid    Id                 { get; set; }
    public Guid    SubscriberId       { get; set; }
    public string  Ruc                { get; set; } = null!;
    public string  LegalName          { get; set; } = null!;
    public string? TradeName          { get; set; }
    public string  MainAddress        { get; set; } = null!;
    public string? Phone              { get; set; }
    public string? Email              { get; set; }
    public string? Website            { get; set; }
    public string  CountryCode        { get; set; } = "ECU";
    /// <summary>IANA timezone (e.g. America/Guayaquil).</summary>
    public string  Timezone           { get; set; } = "America/Guayaquil";
    /// <summary>ISO 4217 currency (e.g. USD).</summary>
    public string  CurrencyCode       { get; set; } = "USD";
    public string? TaxRegimeCode      { get; set; }
    public bool    IsAccountingReq    { get; set; } = false;
    public string? SpecialTaxpayerNo  { get; set; }
    public bool    IsForeignTrade     { get; set; } = false;
    public bool    WithholdsRenta     { get; set; } = true;
    public bool    WithholdsVat       { get; set; } = true;
    // SRI
    public short   EnvironmentCode    { get; set; } = 2;  // 2=Pruebas por defecto
    public short   EmissionTypeCode   { get; set; } = 1;  // 1=Normal
    public string? WsdlRecvTest       { get; set; }
    public string? WsdlAuthTest       { get; set; }
    public string? WsdlRecvProd       { get; set; }
    public string? WsdlAuthProd       { get; set; }
    // UI / white-label
    public string? LogoUrl            { get; set; }
    public string? LogoBase64         { get; set; }
    /// <summary>JSON theme tokens (colors, fonts) for white-label; validated at API layer.</summary>
    public string? BrandingJson       { get; set; }
    public string? ExtraLegend        { get; set; }
    public short   ReceiptWidthMm     { get; set; } = 80;
    public bool    IsActive           { get; set; } = true;
    public DateTime CreatedAt         { get; set; }
    public DateTime UpdatedAt         { get; set; }

    // Navigation
    public SriCountry?      Country      { get; set; }
    public SriTaxRegime?    TaxRegime    { get; set; }
    public SriEnvironment?  Environment  { get; set; }
    public SriEmissionType? EmissionType { get; set; }

    public ICollection<DigitalCertificate> Certificates   { get; set; } = [];
    public ICollection<Establishment>      Establishments { get; set; } = [];
    public ICollection<GeneralParameter>   Parameters     { get; set; } = [];

    /// <summary>Tax identifier (Ecuador: RUC). Alias for API consumers.</summary>
    public string TaxId => Ruc;

    public static Company CreateFromSubscriber(
        Guid subscriberId,
        string ruc,
        string legalName,
        string mainAddress,
        string? tradeName = null,
        string? email = null,
        string? phone = null)
        => CreateManaged(
            subscriberId,
            ruc,
            legalName,
            mainAddress,
            tradeName,
            email,
            phone);

    public static Company CreateManaged(
        Guid subscriberId,
        string ruc,
        string legalName,
        string mainAddress,
        string? tradeName = null,
        string? email = null,
        string? phone = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? logoUrl = null,
        string? brandingJson = null)
    {
        var now = DateTime.UtcNow;
        return new Company
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Ruc = NormalizeRuc(ruc),
            LegalName = legalName.Trim(),
            TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            MainAddress = string.IsNullOrWhiteSpace(mainAddress) ? "—" : mainAddress.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "ECU" : countryCode.Trim().ToUpperInvariant(),
            Timezone = string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant(),
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            BrandingJson = string.IsNullOrWhiteSpace(brandingJson) ? null : brandingJson.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void UpdateProfile(
        string legalName,
        string? tradeName,
        string mainAddress,
        string? phone,
        string? email,
        string countryCode,
        string timezone,
        string currencyCode,
        string? logoUrl,
        string? brandingJson,
        bool isActive)
    {
        LegalName = legalName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        MainAddress = string.IsNullOrWhiteSpace(mainAddress) ? "—" : mainAddress.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? CountryCode : countryCode.Trim().ToUpperInvariant();
        Timezone = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone.Trim();
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? CurrencyCode : currencyCode.Trim().ToUpperInvariant();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        BrandingJson = string.IsNullOrWhiteSpace(brandingJson) ? null : brandingJson.Trim();
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTaxId(string ruc) => Ruc = NormalizeRuc(ruc);

    private static string NormalizeRuc(string ruc)
    {
        var t = ruc.Trim();
        if (t.Length == 13)
            return t;
        if (t.Length < 13)
            return t.PadRight(13, '0')[..13];
        return t[..13];
    }
}
