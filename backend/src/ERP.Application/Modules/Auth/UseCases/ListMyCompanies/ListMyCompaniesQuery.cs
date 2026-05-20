using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.ListMyCompanies;

public sealed record ListMyCompaniesQuery : IRequest<Result<IReadOnlyList<AccessibleCompanyDto>>>;
