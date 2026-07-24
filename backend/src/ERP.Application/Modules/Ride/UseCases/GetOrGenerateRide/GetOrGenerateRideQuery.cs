using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;

namespace ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;

/// <summary>
/// Único punto de entrada público para obtener-o-generar un RIDE (ADR-025 §7). Se identifica
/// por <c>(SourceModule, SourceEntityId)</c> — el dato que ya tiene el módulo consumidor (Sales
/// hoy), nunca el Id interno de <c>ElectronicDocument</c> (corrección H2). Contrato congelado:
/// cualquier cambio de firma requiere una nueva ADR.
/// </summary>
public sealed record GetOrGenerateRideQuery(
    string SourceModule,
    Guid SourceEntityId
) : IRequest<Result<RideGenerationResultDto>>, ICompanyScopedRequest;
