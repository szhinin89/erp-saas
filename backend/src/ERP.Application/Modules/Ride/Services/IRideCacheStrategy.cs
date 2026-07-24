using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Decide si un PDF ya generado sigue siendo válido comparando los 5 valores de huella de
/// ADR-025 §14: hash del XML, versión de plantilla, de branding, de renderer y de
/// especificación de Ride. Un cambio en cualquiera invalida el cache.
/// </summary>
public interface IRideCacheStrategy
{
    /// <summary><see langword="null"/> dentro de un resultado exitoso significa cache-miss — no es un error.</summary>
    Task<Result<RidePdfMetadataDto?>> TryGetCachedAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        RideContentHash sourceXmlHash,
        string templateId,
        string templateVersion,
        string brandingVersion,
        string rendererVersion,
        string rideSpecificationVersion,
        CancellationToken ct = default);
}
