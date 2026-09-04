using ERP.Application.Modules.Ride.Rendering;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Modelo de composición producido por <see cref="RetentionRideTemplate"/> — únicamente datos,
/// sin bytes, sin QuestPDF, sin I/O. Mismo criterio que <see cref="InvoiceRideDocumentLayout"/>.
///
/// <see cref="AuthorizationDateDisplay"/> resuelve el mismo fallback ya usado por
/// <c>HeaderSection</c> (Infrastructure) para Factura/Nota de Crédito cuando
/// <c>Header.AuthorizationDate</c> es <see langword="null"/> ("no disponible") — nunca inventa una
/// fecha de autorización que el XML todavía no tiene. Se expone aquí (en vez de solo en el futuro
/// renderer) para que cualquier consumidor de este layout — incluida una plantilla HTML que no
/// use QuestPDF — obtenga el mismo texto seguro sin reimplementar la regla.
/// </summary>
public sealed class RetentionRideDocumentLayout : IRideDocumentLayout
{
    private const string AuthorizationDatePendingText = "no disponible";

    public RetentionRideHeader Header { get; }
    public RideParty Issuer { get; }
    public RideParty SubjectWithheld { get; }
    public RetentionRideSourceDocument SourceDocument { get; }
    public IReadOnlyList<RetentionRideTaxLine> Lines { get; }
    public decimal TotalRetained { get; }
    public IReadOnlyList<RideAdditionalInfo> AdditionalInfo { get; }
    public RideBranding Branding { get; }
    public string QrPlaceholder { get; }
    public string AuthorizationDateDisplay { get; }

    public RetentionRideDocumentLayout(RetentionRideModel model, RideBranding branding)
    {
        Header = model.Header;
        Issuer = model.Issuer;
        SubjectWithheld = model.SubjectWithheld;
        SourceDocument = model.SourceDocument;
        Lines = model.Lines;
        TotalRetained = model.TotalRetained;
        AdditionalInfo = model.AdditionalInfo;
        Branding = branding;
        QrPlaceholder = model.Header.AccessKey.Value;
        AuthorizationDateDisplay = model.Header.AuthorizationDate.HasValue
            ? model.Header.AuthorizationDate.Value.ToString("dd/MM/yyyy HH:mm:ss")
            : AuthorizationDatePendingText;
    }
}
