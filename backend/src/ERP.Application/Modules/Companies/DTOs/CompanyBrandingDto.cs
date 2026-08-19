namespace ERP.Application.Modules.Companies.DTOs;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: marca de la empresa expuesta a "Configuración → Empresa → Marca"
/// (<c>GET/PUT /api/v1/companies/profile/branding</c>). Tipado — nunca JSON crudo. No incluye el
/// logo: el logo es un archivo (<c>MediaFile</c>) y ya viaja en <see cref="CompanyProfileDto.Logo"/>/
/// <see cref="CompanyProfileDto.AlternateLogo"/>; duplicarlo aquí sería una segunda fuente de
/// verdad para el mismo dato.
/// </summary>
public sealed record CompanyBrandingDto(
    string? PrimaryColor,
    string? SecondaryColor,
    string? Slogan,
    string? DocumentFooterText
);
