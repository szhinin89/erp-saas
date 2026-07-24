using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Única plantilla activa en v1.0 (ADR-025 §11) — compone <see cref="InvoiceRideDocumentLayout"/>
/// sin conocer QuestPDF ni ningún motor de render.
/// </summary>
public sealed class DefaultInvoiceRideTemplate : IRideTemplate
{
    public RideDocumentType DocumentType => RideDocumentType.Invoice;

    public IRideDocumentLayout Compose(RideModel model, RideBranding branding) =>
        new InvoiceRideDocumentLayout(model, branding);
}
