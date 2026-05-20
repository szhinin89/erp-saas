using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.CreateCompany;

public sealed record CreateCompanyCommand(
    string TaxId,
    string LegalName,
    string MainAddress,
    string? TradeName,
    string? Phone,
    string? Email,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    string? LogoUrl,
    string? BrandingJson) : IRequest<Result<CompanyDetailDto>>;
