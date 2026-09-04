using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Caja del código QR — formato oficial del SRI: QR ya generado por
/// <c>IRideQrCodeGenerator</c> (ver <see cref="ERP.Infrastructure.Ride.Rendering.QuestPdfRideRenderer"/>)
/// junto a la clave de acceso, en una única caja con borde. Esta sección solo dibuja los bytes
/// PNG recibidos, nunca genera ni codifica el QR por sí misma.
/// </summary>
public static class QrFooterSection
{
    public static void Compose(
        IContainer container,
        InvoiceRideDocumentLayout layout,
        byte[] qrImageBytes
    ) => Compose(container, layout.QrPlaceholder, layout.Branding, qrImageBytes);

    /// <summary>RETENTIONS-RIDE-PDF-RENDERER-03D — mismo render, para el Comprobante de Retención
    /// (<see cref="RetentionRideDocumentLayout"/> también expone <c>QrPlaceholder</c>/<c>Branding</c>,
    /// misma forma exacta) — evita duplicar esta sección para un segundo tipo de comprobante.</summary>
    public static void Compose(
        IContainer container,
        RetentionRideDocumentLayout layout,
        byte[] qrImageBytes
    ) => Compose(container, layout.QrPlaceholder, layout.Branding, qrImageBytes);

    private static void Compose(
        IContainer container,
        string qrPlaceholder,
        ERP.Domain.Modules.Ride.ValueObjects.RideBranding branding,
        byte[] qrImageBytes
    )
    {
        container
            .RideBox()
            .Padding(6)
            .Row(row =>
            {
                row.ConstantItem(80).Height(80).Image(qrImageBytes).FitArea();

                row.RelativeItem()
                    .PaddingLeft(8)
                    .Column(column =>
                    {
                        column.Item().Text("Clave de Acceso:").Bold().FontSize(8);
                        column.Item().Text(qrPlaceholder).FontSize(7);
                        if (branding.FooterText is { } footerText)
                            column.Item().PaddingTop(4).Text(footerText).FontSize(8);
                    });
            });
    }
}
