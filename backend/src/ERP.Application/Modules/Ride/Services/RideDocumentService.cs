using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Facade interna (ADR-025 §8) — delega toda la orquestación en <see cref="RidePipeline"/>. Sin
/// lógica propia: reemplaza a <c>NullRideDocumentService</c> (Fase 4).
/// </summary>
public sealed class RideDocumentService : IRideDocumentService
{
    private readonly RidePipeline _pipeline;

    public RideDocumentService(RidePipeline pipeline) => _pipeline = pipeline;

    public Task<Result<RideGenerationResultDto>> GetOrGenerateAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId, CancellationToken ct = default)
        => _pipeline.ExecuteAsync(tenantId, companyId, sourceModule, sourceEntityId, forceRegenerate: false, ct);

    public Task<Result<RideGenerationResultDto>> RegenerateAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId, CancellationToken ct = default)
        => _pipeline.ExecuteAsync(tenantId, companyId, sourceModule, sourceEntityId, forceRegenerate: true, ct);
}
