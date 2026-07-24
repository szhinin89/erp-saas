using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;

public sealed record GetCompanyProfileQuery : IRequest<Result<CompanyProfileDto>>;
