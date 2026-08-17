using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;

/// <summary>
/// Contacto, representante legal y configuración regional de la empresa activa
/// ("General" de Company Settings). LegalName/TradeName/RUC no forman parte de este comando:
/// son propiedad exclusiva de Companies Admin (<c>UpdateCompanyCommand</c>).
/// </summary>
public sealed record UpdateCompanyProfileCommand(
    string? CorporateEmail,
    string? Phone,
    string? Website,
    string CurrencyCode,
    string Timezone,
    string? LegalRepName,
    string? LegalRepPosition,
    string? LegalRepIdNumber,
    string? LegalRepEmail,
    string? LegalRepPhone
) : IRequest<Result<CompanyProfileDto>>;
