using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEmissionPoints;

public record GetEmissionPointsByEstablishmentQuery(Guid EstablishmentId)
    : IRequest<Result<IReadOnlyList<EmissionPointDto>>>,
        ICompanyScopedRequest;
