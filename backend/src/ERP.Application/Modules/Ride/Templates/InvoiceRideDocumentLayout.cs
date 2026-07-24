using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Modelo de composición producido por <see cref="DefaultInvoiceRideTemplate"/> — únicamente
/// datos, sin bytes, sin QuestPDF, sin I/O. <see cref="QrPlaceholder"/> es el contenido crudo
/// que un código QR debería codificar (la clave de acceso) — nunca una imagen: la generación
/// visual real del QR es responsabilidad de <c>IRideRenderer</c> en la Fase 7.
/// </summary>
public sealed class InvoiceRideDocumentLayout : IRideDocumentLayout
{
    public RideHeader Header { get; }
    public RideParty Issuer { get; }
    public RideParty Receiver { get; }
    public IReadOnlyList<RideLine> Lines { get; }
    public IReadOnlyList<RideTaxSummary> TaxSummary { get; }
    public IReadOnlyList<RidePaymentInfo> Payments { get; }
    public IReadOnlyList<RideAdditionalInfo> AdditionalInfo { get; }
    public RideBranding Branding { get; }
    public string QrPlaceholder { get; }

    public InvoiceRideDocumentLayout(RideModel model, RideBranding branding)
    {
        Header = model.Header;
        Issuer = model.Issuer;
        Receiver = model.Receiver;
        Lines = model.Lines;
        TaxSummary = model.TaxSummary;
        Payments = model.Payments;
        AdditionalInfo = model.AdditionalInfo;
        Branding = branding;
        QrPlaceholder = model.Header.AccessKey.Value;
    }
}
