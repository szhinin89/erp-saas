using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;

public sealed record UpdateCompanyBrandingCommand(string? BrandingConfiguration)
    : IRequest<Result<CompanyProfileDto>>;
