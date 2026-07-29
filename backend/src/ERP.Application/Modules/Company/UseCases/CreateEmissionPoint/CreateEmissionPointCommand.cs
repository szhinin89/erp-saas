using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Enums;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;

public record CreateEmissionPointCommand(
    Guid EstablishmentId,
    string Code,
    string? Name,
    EmissionType EmissionType,
    bool IsDefault
) : IRequest<Result<EmissionPointDto>>, ICompanyScopedRequest;
