using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Configuration.Constants;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — encabezado del RIDE de Comprobante de Retención. Mismo
/// patrón de tres cajas que <see cref="HeaderSection"/> (Factura/Nota de Crédito): logo + datos
/// del emisor a la izquierda, datos del comprobante a la derecha. Consume únicamente
/// <c>Issuer</c>/<c>Header</c> de <see cref="RetentionRideDocumentLayout"/> y los bytes de
/// logo/código de barras ya resueltos por
/// <see cref="ERP.Infrastructure.Ride.Rendering.QuestPdfRideRenderer"/> — nunca lee storage,
/// branding ni el Building Block Codes directamente.
///
/// La fecha de autorización usa <c>layout.AuthorizationDateDisplay</c> (ya resuelto por el
/// layout, RETENTIONS-RIDE-TEMPLATE-03C) en vez de reimplementar el fallback "no disponible" —
/// una sola fuente de esa regla, no duplicada como en <see cref="HeaderSection"/>.
/// </summary>
public static class RetentionHeaderSection
{
    public static void Compose(
        IContainer container,
        RetentionRideDocumentLayout layout,
        byte[]? logoBytes,
        byte[] barcodeImageBytes
    )
    {
        var issuer = layout.Issuer;
        var header = layout.Header;

        container.Row(row =>
        {
            row.RelativeItem(6)
                .Column(left =>
                {
                    left.Spacing(4);

                    left.Item()
                        .RideBox()
                        .Height(40)
                        .Padding(4)
                        .AlignMiddle()
                        .Element(logo =>
                        {
                            if (logoBytes is not null)
                                logo.AlignLeft().Image(logoBytes).FitArea();
                            else
                                logo.AlignLeft()
                                    .Text("NO TIENE LOGO")
                                    .Bold()
                                    .FontColor(Colors.Red.Medium)
                                    .FontSize(14);
                        });

                    left.Item()
                        .RideBox()
                        .Padding(8)
                        .Column(info =>
                        {
                            info.Spacing(2);

                            info.Item().Text(issuer.LegalName).Bold().FontSize(11);
                            if (issuer.TradeName is not null)
                                info.Item().Text(issuer.TradeName).FontSize(8);
                            if (issuer.Address is not null)
                                info.Item().Text($"Dirección Matriz: {issuer.Address}").FontSize(8);
                            info.Item()
                                .Text($"Dirección Sucursal: {header.EstablishmentAddress}")
                                .FontSize(8);
                            if (issuer.IsAccountingRequired.HasValue)
                                info.Item()
                                    .Text(
                                        $"Obligado a Llevar Contabilidad: {(issuer.IsAccountingRequired.Value ? "SI" : "NO")}"
                                    )
                                    .FontSize(8);
                            if (issuer.TaxRegime is not null)
                                info.Item().PaddingTop(4).Text(issuer.TaxRegime).FontSize(8);
                        });
                });

            row.RelativeItem(5)
                .RideBox()
                .Padding(8)
                .Column(right =>
                {
                    right.Spacing(2);

                    right.Item().Text($"R.U.C.: {issuer.IdentificationNumber}").Bold().FontSize(10);
                    right
                        .Item()
                        .PaddingTop(4)
                        .Text("COMPROBANTE DE RETENCIÓN")
                        .Bold()
                        .FontSize(12);
                    right
                        .Item()
                        .PaddingTop(4)
                        .Text(
                            $"No. {header.Establishment}-{header.EmissionPoint}-{header.Sequential}"
                        )
                        .FontSize(8);
                    right.Item().PaddingTop(4).Text("NÚMERO DE AUTORIZACIÓN").Bold().FontSize(8);
                    right.Item().Text(header.AuthorizationNumber).FontSize(7);
                    right
                        .Item()
                        .PaddingTop(4)
                        .Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8));
                            text.Span("FECHA Y HORA DE AUTORIZACIÓN: ").Bold();
                            text.Span(layout.AuthorizationDateDisplay);
                        });
                    right
                        .Item()
                        .Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8));
                            text.Span("AMBIENTE: ").Bold();
                            text.Span(
                                SriEnvironmentCodes.IsProduction(header.Environment)
                                    ? "PRODUCCIÓN"
                                    : "PRUEBAS"
                            );
                        });
                    right
                        .Item()
                        .Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8));
                            text.Span("EMISIÓN: ").Bold();
                            text.Span(
                                header.EmissionType == "1"
                                    ? "NORMAL"
                                    : "INDISPONIBILIDAD DEL SISTEMA"
                            );
                        });
                    right.Item().PaddingTop(4).Text("CLAVE DE ACCESO").Bold().FontSize(8);
                    right.Item().PaddingTop(2).Image(barcodeImageBytes).FitWidth();
                    right.Item().AlignCenter().Text(header.AccessKey.Value).FontSize(7);
                });
        });
    }
}
