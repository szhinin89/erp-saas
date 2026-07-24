using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.Company.Enums;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Empresa emisora de comprobantes electrónicos (RUC).
/// Un Tenant puede tener N companies (holdings / franquicias).
/// Tablas SRI y documentos electrónicos referencian <c>company_id</c>.
/// </summary>
public class Company : ITenantScopedEntity
{
    public const int CorporateEmailMaxLen = 120;
    public const int WebsiteMaxLen        = 200;

    public Guid    Id                            { get; set; }
    public Guid    TenantId                      { get; set; }
    public string  TaxIdentificationNumber       { get; set; } = null!;
    public bool    IsTemporaryTaxIdentification  { get; set; }
    public TaxIdentificationStatus TaxIdentificationStatus { get; set; } = TaxIdentificationStatus.Verified;
    public string  LegalName                     { get; set; } = null!;
    public string? TradeName                     { get; set; }
    public string? CorporateEmail    { get; set; }
    public string? Phone             { get; set; }
    public string? Website           { get; set; }
    public string  CountryCode        { get; set; } = "ECU";
    /// <summary>IANA timezone (e.g. America/Guayaquil).</summary>
    public string  Timezone           { get; set; } = "America/Guayaquil";
    /// <summary>ISO 4217 currency (e.g. USD).</summary>
    public string  CurrencyCode       { get; set; } = "USD";
    public string? TaxRegimeCode      { get; set; }
    public bool    IsAccountingReq    { get; set; }
    public string? SpecialTaxpayerNo  { get; set; }
    public bool    IsForeignTrade     { get; set; }
    public bool    WithholdsRenta     { get; set; } = true;
    public bool    WithholdsVat       { get; set; } = true;
    // UI / white-label
    /// <summary>JSON theme tokens (colors, fonts) para white-label; validado en capa Application.</summary>
    public string? BrandingConfiguration { get; set; }
    public string? ExtraLegend        { get; set; }
    /// <summary>Idioma principal de la empresa (es/en/qu).</summary>
    public string  LanguageCode       { get; set; } = "es";
    // Representante legal
    public string? LegalRepName       { get; set; }
    public string? LegalRepPosition   { get; set; }
    public string? LegalRepIdNumber   { get; set; }
    public string? LegalRepEmail      { get; set; }
    public string? LegalRepPhone      { get; set; }
    public bool    IsActive           { get; set; } = true;
    public DateTime  CreatedAt        { get; set; }
    public DateTime  UpdatedAt        { get; set; }
    public Guid?     CreatedBy        { get; set; }
    public Guid?     UpdatedBy        { get; set; }

    // ── Onboarding & Operational Status ──────────────────────────────────────

    /// <summary>
    /// Histórico del wizard de onboarding (eliminado en la limpieza "ERP pure").
    /// Las companies quedan operativas de inmediato al provisionarse; este campo
    /// se conserva por compatibilidad de esquema/contrato de integración con Platform.
    /// </summary>
    public bool                    OnboardingCompleted { get; private set; }

    /// <summary>
    /// Operational: empresa lista para operar. Suspended: suspendida por el operador platform.
    /// </summary>
    public CompanyOperationalStatus OperationalStatus   { get; private set; } = CompanyOperationalStatus.Operational;

    // Navigation
    public SriCountry?      Country      { get; set; }
    public SriTaxRegime?    TaxRegime    { get; set; }

    public ICollection<Establishment>      Establishments { get; set; } = [];
    public ICollection<GeneralParameter>   Parameters     { get; set; } = [];

    // ── Lifecycle methods ─────────────────────────────────────────────────────

    /// <summary>Marks company as fully operational (alias for use after admin re-enables).</summary>
    public void MarkOperational()
    {
        OperationalStatus = CompanyOperationalStatus.Operational;
        UpdatedAt         = DateTime.UtcNow;
    }

    /// <summary>Suspends company ERP operations (e.g., billing issue, platform action).</summary>
    public void SuspendOperations()
    {
        OperationalStatus = CompanyOperationalStatus.Suspended;
        UpdatedAt         = DateTime.UtcNow;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    public static Company CreateFromTenant(
        Guid tenantId,
        string taxIdentificationNumber,
        string legalName,
        string? tradeName = null,
        string? corporateEmail = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD")
        => CreateManaged(
            tenantId,
            taxIdentificationNumber,
            legalName,
            tradeName,
            corporateEmail,
            countryCode,
            timezone,
            currencyCode);

    public static Company CreateManaged(
        Guid tenantId,
        string taxIdentificationNumber,
        string legalName,
        string? tradeName = null,
        string? corporateEmail = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? brandingConfiguration = null,
        string? website = null,
        bool isTemporaryTaxIdentification = false,
        TaxIdentificationStatus taxIdentificationStatus = TaxIdentificationStatus.Verified,
        Guid? createdBy = null)
    {
        var now = DateTime.UtcNow;
        return new Company
        {
            Id                           = Guid.NewGuid(),
            TenantId                     = tenantId,
            TaxIdentificationNumber      = NormalizeTaxIdentificationNumber(taxIdentificationNumber, isTemporaryTaxIdentification),
            IsTemporaryTaxIdentification = isTemporaryTaxIdentification,
            TaxIdentificationStatus      = taxIdentificationStatus,
            LegalName                    = legalName.Trim(),
            TradeName                    = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            CorporateEmail               = NormalizeEmail(corporateEmail),
            Website                      = NormalizeWebsite(website),
            CountryCode                  = string.IsNullOrWhiteSpace(countryCode) ? "ECU" : countryCode.Trim().ToUpperInvariant(),
            Timezone                     = string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone.Trim(),
            CurrencyCode                 = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant(),
            BrandingConfiguration        = string.IsNullOrWhiteSpace(brandingConfiguration) ? null : brandingConfiguration.Trim(),
            IsActive                     = true,
            OnboardingCompleted          = true,
            OperationalStatus            = CompanyOperationalStatus.Operational,
            CreatedAt                    = now,
            UpdatedAt                    = now,
            CreatedBy                    = createdBy,
        };
    }

    public void UpdateProfile(
        string legalName,
        string? tradeName,
        string? corporateEmail,
        string? website,
        string countryCode,
        string timezone,
        string currencyCode,
        Guid? updatedBy,
        string? phone = null,
        string? legalRepName = null,
        string? legalRepPosition = null,
        string? legalRepIdNumber = null,
        string? legalRepEmail = null,
        string? legalRepPhone = null)
    {
        LegalName        = legalName.Trim();
        TradeName        = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        CorporateEmail   = NormalizeEmail(corporateEmail);
        Phone            = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Website          = NormalizeWebsite(website);
        CountryCode      = string.IsNullOrWhiteSpace(countryCode) ? CountryCode : countryCode.Trim().ToUpperInvariant();
        Timezone         = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone.Trim();
        CurrencyCode     = string.IsNullOrWhiteSpace(currencyCode) ? CurrencyCode : currencyCode.Trim().ToUpperInvariant();
        LegalRepName     = string.IsNullOrWhiteSpace(legalRepName) ? null : legalRepName.Trim();
        LegalRepPosition = string.IsNullOrWhiteSpace(legalRepPosition) ? null : legalRepPosition.Trim();
        LegalRepIdNumber = string.IsNullOrWhiteSpace(legalRepIdNumber) ? null : legalRepIdNumber.Trim();
        LegalRepEmail    = NormalizeEmail(legalRepEmail);
        LegalRepPhone    = string.IsNullOrWhiteSpace(legalRepPhone) ? null : legalRepPhone.Trim();
        UpdatedAt        = DateTime.UtcNow;
        UpdatedBy        = updatedBy;
    }

    /// <summary>Overload retained for the company-management module, which also manages <c>IsActive</c> and <c>BrandingConfiguration</c>.</summary>
    public void UpdateProfile(
        string legalName,
        string? tradeName,
        string? corporateEmail,
        string? website,
        string countryCode,
        string timezone,
        string currencyCode,
        string? brandingConfiguration,
        bool isActive,
        Guid? updatedBy)
    {
        UpdateProfile(legalName, tradeName, corporateEmail, website, countryCode, timezone, currencyCode, updatedBy);
        BrandingConfiguration = string.IsNullOrWhiteSpace(brandingConfiguration) ? null : brandingConfiguration.Trim();
        IsActive              = isActive;
    }

    public void UpdateTaxIdentification(string taxIdentificationNumber, bool isTemporary, TaxIdentificationStatus status, Guid? updatedBy = null)
    {
        TaxIdentificationNumber      = NormalizeTaxIdentificationNumber(taxIdentificationNumber, isTemporary);
        IsTemporaryTaxIdentification = isTemporary;
        TaxIdentificationStatus      = status;
        UpdatedAt                    = DateTime.UtcNow;
        UpdatedBy                    = updatedBy ?? UpdatedBy;
    }

    /// <summary>Actualiza la configuración fiscal (pestaña "Fiscal" de Configuración → Empresa).</summary>
    public void UpdateFiscalSettings(
        string? taxRegimeCode,
        bool isAccountingReq,
        string? specialTaxpayerNo,
        bool isForeignTrade,
        bool withholdsRenta,
        bool withholdsVat,
        Guid? updatedBy)
    {
        TaxRegimeCode     = string.IsNullOrWhiteSpace(taxRegimeCode) ? null : taxRegimeCode.Trim().ToUpperInvariant();
        IsAccountingReq   = isAccountingReq;
        SpecialTaxpayerNo = string.IsNullOrWhiteSpace(specialTaxpayerNo) ? null : specialTaxpayerNo.Trim();
        IsForeignTrade    = isForeignTrade;
        WithholdsRenta    = withholdsRenta;
        WithholdsVat      = withholdsVat;
        UpdatedAt         = DateTime.UtcNow;
        UpdatedBy         = updatedBy;
    }

    /// <summary>Actualiza el idioma principal (pestaña "Operación" de Configuración → Empresa).</summary>
    public void UpdateOperationSettings(string languageCode, Guid? updatedBy)
    {
        LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? LanguageCode : languageCode.Trim().ToLowerInvariant();
        UpdatedAt    = DateTime.UtcNow;
        UpdatedBy    = updatedBy;
    }

    /// <summary>Actualiza las notas legales institucionales (pestaña "Documentos" de Configuración → Empresa).</summary>
    public void UpdateDocumentsSettings(string? extraLegend, Guid? updatedBy)
    {
        ExtraLegend = string.IsNullOrWhiteSpace(extraLegend) ? null : extraLegend.Trim();
        UpdatedAt   = DateTime.UtcNow;
        UpdatedBy   = updatedBy;
    }

    /// <summary>Actualiza colores corporativos y eslogan (pestaña "Marca" de Configuración → Empresa).</summary>
    public void UpdateBrandingConfiguration(string? brandingConfigurationJson, Guid? updatedBy)
    {
        BrandingConfiguration = string.IsNullOrWhiteSpace(brandingConfigurationJson) ? null : brandingConfigurationJson.Trim();
        UpdatedAt             = DateTime.UtcNow;
        UpdatedBy             = updatedBy;
    }

    private static string NormalizeTaxIdentificationNumber(string taxIdentificationNumber, bool isTemporary)
    {
        var t = taxIdentificationNumber.Trim();
        if (isTemporary) return t;
        if (t.Length == 13) return t;
        if (t.Length < 13)  return t.PadRight(13, '0')[..13];
        return t[..13];
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(e)) return null;
        if (e.Length > CorporateEmailMaxLen)
            throw new ArgumentException($"El email no puede superar {CorporateEmailMaxLen} caracteres.", nameof(email));
        if (!e.Contains('@') || e.IndexOf('.', e.IndexOf('@')) < 0)
            throw new ArgumentException("Formato de email inválido.", nameof(email));
        return e;
    }

    private static string? NormalizeWebsite(string? website)
    {
        var w = website?.Trim();
        if (string.IsNullOrEmpty(w)) return null;
        if (w.Length > WebsiteMaxLen)
            throw new ArgumentException($"El sitio web no puede superar {WebsiteMaxLen} caracteres.", nameof(website));
        return w;
    }
}
