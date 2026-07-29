namespace ERP.Application.Modules.Ride.DTOs;

/// <summary>
/// Respuesta única de <c>GetOrGenerateRideQuery</c>/<c>RegenerateRideCommand</c> (ADR-025 §7).
/// Contrato público congelado — cualquier cambio de firma requiere una nueva ADR.
/// </summary>
public sealed record RideGenerationResultDto(
    RideOutcome Outcome,
    string? StoragePath,
    RidePdfMetadataDto? Metadata,
    string? ReasonCode
);
