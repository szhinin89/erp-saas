using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed record CreateCompanyCommand(
    Guid TenantId,
    string TaxId,
    string LegalName,
    string? TradeName
) : IRequest<Result<CompanyDetailDto>>;
