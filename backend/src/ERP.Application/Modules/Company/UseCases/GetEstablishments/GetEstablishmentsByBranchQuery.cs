using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

public record GetEstablishmentsByBranchQuery(Guid BranchId) : IRequest<Result<IReadOnlyList<EstablishmentDto>>>, ICompanyScopedRequest;
