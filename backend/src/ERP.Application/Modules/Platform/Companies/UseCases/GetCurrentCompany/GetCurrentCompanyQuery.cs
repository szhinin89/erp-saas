using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.GetCurrentCompany;

public sealed record GetCurrentCompanyQuery : IRequest<Result<CompanyDetailDto>>;
