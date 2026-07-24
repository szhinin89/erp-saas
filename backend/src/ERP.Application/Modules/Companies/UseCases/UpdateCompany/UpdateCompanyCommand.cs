using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

public sealed record UpdateCompanyCommand(
    Guid Id,
    string LegalName,
    string? TradeName,
    string? CorporateEmail,
    string? Website,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    string? BrandingJson,
    bool IsActive,
    string? TaxId = null) : IRequest<Result<CompanyDetailDto>>;
