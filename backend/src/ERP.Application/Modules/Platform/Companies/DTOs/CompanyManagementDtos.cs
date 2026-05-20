namespace ERP.Application.Modules.Platform.Companies.DTOs;

public sealed record CompanyListItemDto(
    Guid Id,
    Guid SubscriberId,
    string LegalName,
    string? TradeName,
    string TaxId,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    bool IsActive,
    string Role);

public sealed record CompanyDetailDto(
    Guid Id,
    Guid SubscriberId,
    string LegalName,
    string? TradeName,
    string TaxId,
    string MainAddress,
    string? Phone,
    string? Email,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    string? LogoUrl,
    string? BrandingJson,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CompanyDetailDto FromEntity(Domain.Modules.Company.Entities.Company c) =>
        new(
            c.Id,
            c.SubscriberId,
            c.LegalName,
            c.TradeName,
            c.Ruc,
            c.MainAddress,
            c.Phone,
            c.Email,
            c.CountryCode,
            c.Timezone,
            c.CurrencyCode,
            c.LogoUrl,
            c.BrandingJson,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt);
}
