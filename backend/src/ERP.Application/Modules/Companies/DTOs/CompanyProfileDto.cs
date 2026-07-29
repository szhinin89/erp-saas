using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Media.Entities;

namespace ERP.Application.Modules.Companies.DTOs;

/// <summary>Logo activo de la empresa, resuelto desde el módulo Media.</summary>
public sealed record CompanyLogoDto(Guid Id, string Url, DateTime LastUpdatedAt)
{
    public const string ContentRoute = "/api/v1/companies/profile/logo/content";
    public const string AlternateContentRoute = "/api/v1/companies/profile/logo-alt/content";

    public static CompanyLogoDto FromMediaFile(MediaFile media) =>
        new(media.Id, ContentRoute, media.UpdatedAt ?? media.CreatedAt);

    public static CompanyLogoDto FromAlternateMediaFile(MediaFile media) =>
        new(media.Id, AlternateContentRoute, media.UpdatedAt ?? media.CreatedAt);
}

/// <summary>
/// Perfil de empresa para "Configuración → Empresa": identidad legal/fiscal,
/// información corporativa, representante legal, configuración fiscal,
/// operación y documentos. <see cref="BrandingConfiguration"/> es un jsonb con
/// la forma <c>{ primaryColor, secondaryColor, slogan }</c>, editado desde la pestaña "Marca".
/// </summary>
public sealed record CompanyProfileDto(
    Guid Id,
    string TaxIdentificationNumber,
    TaxIdentificationStatus TaxIdentificationStatus,
    bool IsTemporaryTaxIdentification,
    string LegalName,
    string? TradeName,
    string? CorporateEmail,
    string? Phone,
    string? Website,
    string CountryCode,
    string CurrencyCode,
    string Timezone,
    string? LegalRepName,
    string? LegalRepPosition,
    string? LegalRepIdNumber,
    string? LegalRepEmail,
    string? LegalRepPhone,
    string? TaxRegimeCode,
    bool IsAccountingReq,
    string? SpecialTaxpayerNo,
    bool IsForeignTrade,
    bool WithholdsRenta,
    bool WithholdsVat,
    string LanguageCode,
    string OperationalStatus,
    bool OnboardingCompleted,
    string? ExtraLegend,
    string? BrandingConfiguration,
    CompanyLogoDto? Logo,
    CompanyLogoDto? AlternateLogo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy
)
{
    public static CompanyProfileDto FromEntity(
        Domain.Modules.Company.Entities.Company c,
        MediaFile? logo = null,
        MediaFile? alternateLogo = null
    ) =>
        new(
            c.Id,
            c.TaxIdentificationNumber,
            c.TaxIdentificationStatus,
            c.IsTemporaryTaxIdentification,
            c.LegalName,
            c.TradeName,
            c.CorporateEmail,
            c.Phone,
            c.Website,
            c.CountryCode,
            c.CurrencyCode,
            c.Timezone,
            c.LegalRepName,
            c.LegalRepPosition,
            c.LegalRepIdNumber,
            c.LegalRepEmail,
            c.LegalRepPhone,
            c.TaxRegimeCode,
            c.IsAccountingReq,
            c.SpecialTaxpayerNo,
            c.IsForeignTrade,
            c.WithholdsRenta,
            c.WithholdsVat,
            c.LanguageCode,
            c.OperationalStatus.ToString(),
            c.OnboardingCompleted,
            c.ExtraLegend,
            c.BrandingConfiguration,
            logo is null ? null : CompanyLogoDto.FromMediaFile(logo),
            alternateLogo is null ? null : CompanyLogoDto.FromAlternateMediaFile(alternateLogo),
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt,
            c.CreatedBy,
            c.UpdatedBy
        );
}
