using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyDocuments;

public sealed record UpdateCompanyDocumentsCommand(string? ExtraLegend) : IRequest<Result<CompanyProfileDto>>;
