using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCurrentCompany;

public sealed record GetCurrentCompanyQuery : IRequest<Result<CompanyDetailDto>>;
