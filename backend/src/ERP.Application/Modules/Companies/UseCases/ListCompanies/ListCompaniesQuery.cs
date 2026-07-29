using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.ListCompanies;

public sealed record ListCompaniesQuery(bool ActiveOnly = true)
    : IRequest<Result<IReadOnlyList<CompanyListItemDto>>>;
