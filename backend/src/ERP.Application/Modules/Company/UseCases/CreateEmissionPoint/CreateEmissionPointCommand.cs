using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;

namespace ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;

public record CreateEmissionPointCommand(
    Guid    EstablishmentId,
    string  Code,
    string? Name,
    bool    IsDefault
) : IRequest<Result<EmissionPointDto>>, ICompanyScopedRequest;
