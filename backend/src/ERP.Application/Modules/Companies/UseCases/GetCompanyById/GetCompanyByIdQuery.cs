using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetCompanyById;

public sealed record GetCompanyByIdQuery(Guid Id) : IRequest<Result<CompanyDetailDto>>;
