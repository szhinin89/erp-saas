using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Compara la huella completa de ADR-025 §14 (hash del XML + versiones de plantilla, branding,
/// renderer y especificación) contra <see cref="IRidePdfDocumentRepository"/> — reemplaza a
/// <c>NullRideCacheStrategy</c> (Fase 4). Solo una huella en estado <see cref="RidePdfState.Generated"/>
/// cuenta como cache-hit: un intento <see cref="RidePdfState.Failed"/>/<see cref="RidePdfState.PendingSource"/>
/// para la misma huella nunca se sirve como si fuera un PDF válido.
///
/// <c>templateId</c> no participa en la búsqueda: <see cref="IRidePdfDocumentRepository.GetByFingerprintAsync"/>
/// (Fase 2, congelado) no lo incluye en su clave de búsqueda — hueco real declarado en la
/// auditoría de esta fase, sin impacto hoy porque <c>TemplateId</c> siempre es una función
/// determinística de <see cref="RideDocumentType"/> mientras exista una sola plantilla por tipo.
/// </summary>
public sealed class RideCacheStrategy : IRideCacheStrategy
{
    private readonly IRidePdfDocumentRepository _repository;

    public RideCacheStrategy(IRidePdfDocumentRepository repository) => _repository = repository;

    public async Task<Result<RidePdfMetadataDto?>> TryGetCachedAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        RideContentHash sourceXmlHash,
        string templateId,
        string templateVersion,
        string brandingVersion,
        string rendererVersion,
        string rideSpecificationVersion,
        CancellationToken ct = default)
    {
        var document = await _repository.GetByFingerprintAsync(
            tenantId, electronicDocumentId, sourceXmlHash,
            templateVersion, brandingVersion, rendererVersion, rideSpecificationVersion, ct);

        if (document is null || document.State != RidePdfState.Generated)
            return Result<RidePdfMetadataDto?>.Success(null);

        var metadata = new RidePdfMetadataDto(
            document.TemplateId,
            document.TemplateVersion,
            document.BrandingVersion,
            document.RendererVersion,
            document.SourceXmlHash.Value,
            document.GeneratedAtUtc!.Value,
            WasCached: true);

        return Result<RidePdfMetadataDto?>.Success(metadata);
    }
}
