using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;

public sealed record UpdateCompanyProfileCommand(
    string  LegalName,
    string? TradeName,
    string? TaxIdentificationNumber,
    string? CorporateEmail,
    string? Phone,
    string? Website,
    string  CurrencyCode,
    string  Timezone,
    string? LegalRepName,
    string? LegalRepPosition,
    string? LegalRepIdNumber,
    string? LegalRepEmail,
    string? LegalRepPhone) : IRequest<Result<CompanyProfileDto>>;
