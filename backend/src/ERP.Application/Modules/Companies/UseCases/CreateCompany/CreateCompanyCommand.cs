using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed record CreateCompanyCommand(
    string TaxId,
    string LegalName,
    string? TradeName,
    string? CorporateEmail,
    string? Website,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    string? BrandingJson) : IRequest<Result<CompanyDetailDto>>;
