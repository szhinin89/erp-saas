using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;

namespace ERP.Application.Modules.Ride.UseCases.RegenerateRide;

/// <summary>
/// Fuerza la regeneración explícita de un RIDE aunque el cache siga siendo válido (ADR-025 §7).
/// Misma identidad que <c>GetOrGenerateRideQuery</c> — <c>(SourceModule, SourceEntityId)</c>.
/// Contrato congelado: cualquier cambio de firma requiere una nueva ADR.
/// </summary>
public sealed record RegenerateRideCommand(
    string SourceModule,
    Guid SourceEntityId
) : IRequest<Result<RideGenerationResultDto>>, ICompanyScopedRequest;
