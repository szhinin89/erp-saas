using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.UpdateCompany;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;

/// <summary>
/// Razón social, nombre comercial, contacto, representante legal y configuración regional de la
/// empresa activa ("General" de Company Settings). El RUC y el estado activo/inactivo no forman
/// parte de este comando: solo los edita Admin Global (<c>UpdateCompanyForAdminCoreCommand</c>).
/// La empresa destino siempre es la activa del token; el comando no lleva identificador.
/// </summary>
public sealed record UpdateCompanyProfileCommand(
    string LegalName,
    string? TradeName,
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
) : IRequest<Result<CompanyProfileDto>>, ICompanyNamesInput;
