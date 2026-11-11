namespace ERP.Application.Modules.Companies.DTOs;

/// <summary>
/// Identidad de la empresa: razón social, RUC y estado (resultado de la edición de Admin Global y
/// de <c>GET /api/v1/companies/current</c>). Contacto/branding/locale son de Company Settings
/// (<see cref="CompanyProfileDto"/>). CountryCode/Timezone/CurrencyCode son informativos.
/// </summary>
public sealed record CompanyDetailDto(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string? TradeName,
    string TaxId,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
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
            c.CountryCode,
            c.Timezone,
            c.CurrencyCode,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt
        );
}
