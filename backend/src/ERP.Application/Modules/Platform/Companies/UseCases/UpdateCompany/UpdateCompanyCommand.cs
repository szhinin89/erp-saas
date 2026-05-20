using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.UpdateCompany;

public sealed record UpdateCompanyCommand(
    Guid Id,
    string LegalName,
    string? TradeName,
    string MainAddress,
    string? Phone,
    string? Email,
    string CountryCode,
    string Timezone,
    string CurrencyCode,
    string? LogoUrl,
    string? BrandingJson,
    bool IsActive,
    string? TaxId = null) : IRequest<Result<CompanyDetailDto>>;
