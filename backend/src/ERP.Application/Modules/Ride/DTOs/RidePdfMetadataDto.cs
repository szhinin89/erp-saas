namespace ERP.Application.Modules.Ride.DTOs;

/// <summary>
/// Metadatos de una generación de RIDE (ADR-025 §7/§14) — permite auditar exactamente con qué
/// huella (XML + plantilla + branding + renderer + especificación) se generó un PDF ya emitido,
/// sin necesidad de regenerarlo para saberlo. Contrato público congelado.
/// </summary>
public sealed record RidePdfMetadataDto(
    string TemplateId,
    string TemplateVersion,
    string BrandingVersion,
    string RendererVersion,
    string SourceXmlHash,
    DateTime GeneratedAtUtc,
    bool WasCached
);
