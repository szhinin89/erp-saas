using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Plantilla de Nota de Crédito (ADR-031 addendum, Fase 12 de P0-01). Compone el mismo
/// <see cref="InvoiceRideDocumentLayout"/> que <see cref="DefaultInvoiceRideTemplate"/> — no un
/// tipo de layout nuevo — porque su forma (Header/Issuer/Receiver/Lines/TaxSummary/Payments/
/// AdditionalInfo/Branding/QrPlaceholder) ya representa correctamente todos los datos de una Nota
/// de Crédito una vez que <see cref="RideModel"/> los trae (incluidos <c>Header.Reason</c> y
/// <c>Header.ModifiedDocument</c>, agregados en esta misma fase). Esto reutiliza sin duplicar el
/// único motor de render existente (<c>QuestPdfRideRenderer</c> selecciona por la FORMA del
/// layout, no por <c>RideDocumentType</c> — ver su propio comentario) y evita crear un segundo
/// <c>IRideDocumentLayout</c>/pipeline de generación PDF paralelo.
/// </summary>
public sealed class CreditNoteRideTemplate : IRideTemplate
{
    public RideDocumentType DocumentType => RideDocumentType.CreditNote;

    public IRideDocumentLayout Compose(RideModel model, RideBranding branding) =>
        new InvoiceRideDocumentLayout(model, branding);
}
