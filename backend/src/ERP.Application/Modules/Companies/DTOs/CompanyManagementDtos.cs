namespace ERP.Application.Modules.Companies.DTOs;

public sealed record CompanyListItemDto(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string? TradeName,
    string TaxId,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    bool IsActive,
    string Role
);

public sealed record CompanyDetailDto(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string? TradeName,
    string TaxId,
    string? CorporateEmail,
    string? Website,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    string? BrandingJson,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static CompanyDetailDto FromEntity(Domain.Modules.Company.Entities.Company c) =>
        new(
            c.Id,
            c.TenantId,
            c.LegalName,
            c.TradeName,
            c.TaxIdentificationNumber,
            c.CorporateEmail,
            c.Website,
            c.CountryCode,
            c.Timezone,
            c.CurrencyCode,
            c.BrandingConfiguration,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt
        );
}
