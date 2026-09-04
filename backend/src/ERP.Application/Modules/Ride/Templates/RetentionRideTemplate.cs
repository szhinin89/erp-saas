using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — plantilla de Comprobante de Retención. Compone
/// <see cref="RetentionRideDocumentLayout"/> a partir de <see cref="RetentionRideModel"/>, sin
/// conocer QuestPDF ni ningún motor de render (mismo criterio que <see cref="DefaultInvoiceRideTemplate"/>/
/// <see cref="CreditNoteRideTemplate"/>).
/// </summary>
public sealed class RetentionRideTemplate : IRetentionRideTemplate
{
    public RideDocumentType DocumentType => RideDocumentType.Retention;

    public IRideDocumentLayout Compose(RetentionRideModel model, RideBranding branding) =>
        new RetentionRideDocumentLayout(model, branding);
}
