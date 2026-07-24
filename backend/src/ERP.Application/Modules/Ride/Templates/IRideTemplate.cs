using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Strategy (ADR-025 §9): compone un <see cref="RideModel"/> ya parseado y un
/// <see cref="RideBranding"/> ya resuelto en un <see cref="IRideDocumentLayout"/> — nunca vuelve
/// a tocar el XML ni conoce QuestPDF (eso es responsabilidad exclusiva de <c>IRideRenderer</c>).
/// </summary>
public interface IRideTemplate
{
    RideDocumentType DocumentType { get; }

    IRideDocumentLayout Compose(RideModel model, RideBranding branding);
}
