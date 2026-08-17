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

/// <summary>
/// Vista administrativa de Companies Admin (<c>/companies</c>): identidad legal, RUC y estado.
/// Contacto/branding/locale son propiedad de Company Settings (<see cref="CompanyProfileDto"/>)
/// y no se exponen aquí para evitar el mismo dato editable desde dos pantallas.
/// CountryCode/Timezone/CurrencyCode se conservan de solo lectura: son informativos para
/// <c>CurrentCompanyCard</c>, no editables desde este módulo.
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
