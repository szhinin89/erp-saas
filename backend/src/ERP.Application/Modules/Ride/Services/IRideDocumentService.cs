using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Facade interna consumida únicamente por <c>GetOrGenerateRideQueryHandler</c> y
/// <c>RegenerateRideCommandHandler</c> — nunca expuesta directamente a otros módulos (la
/// superficie pública real son esos dos requests de MediatR, ADR-025 §7). Orquesta
/// <c>RidePipeline</c> (Fase 5 del plan de implementación); en esta fase es únicamente contrato.
/// </summary>
public interface IRideDocumentService
{
    Task<Result<RideGenerationResultDto>> GetOrGenerateAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId, CancellationToken ct = default);

    Task<Result<RideGenerationResultDto>> RegenerateAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId, CancellationToken ct = default);
}
