using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;

namespace ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;

public record UpdateEmissionPointCommand(
    Guid    Id,
    string? Name,
    bool    IsDefault
) : IRequest<Result<EmissionPointDto>>, ICompanyScopedRequest;
