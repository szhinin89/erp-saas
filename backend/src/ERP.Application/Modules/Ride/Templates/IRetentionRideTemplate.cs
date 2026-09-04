using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — compone un <see cref="RetentionRideModel"/> ya parseado y un
/// <see cref="RideBranding"/> ya resuelto en un <see cref="IRideDocumentLayout"/>.
///
/// Deliberadamente NO implementa <see cref="IRideTemplate"/> (esa interfaz está fija a
/// <c>RideModel</c>, forma comercial incompatible con <c>RetentionRideModel</c>) ni se registra en
/// <see cref="IRideTemplateResolver"/> — mismo criterio de fork que <see cref="Parsers.IRetentionRideXmlParser"/>.
/// El wiring final con <c>RidePipeline</c>/<c>QuestPdfRideRenderer</c> queda pendiente de esta fase.
/// </summary>
public interface IRetentionRideTemplate
{
    RideDocumentType DocumentType { get; }

    IRideDocumentLayout Compose(RetentionRideModel model, RideBranding branding);
}
