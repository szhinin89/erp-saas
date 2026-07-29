using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyOperation;

public sealed record UpdateCompanyOperationCommand(string LanguageCode)
    : IRequest<Result<CompanyProfileDto>>;
