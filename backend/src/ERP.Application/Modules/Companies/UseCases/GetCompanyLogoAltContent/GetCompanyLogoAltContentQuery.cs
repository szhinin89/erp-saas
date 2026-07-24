using ERP.Application.Common;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoContent;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyLogoAltContent;

public sealed record GetCompanyLogoAltContentQuery : IRequest<Result<CompanyLogoContent>>;
