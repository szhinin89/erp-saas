using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — datos del sujeto retenido y período fiscal, caja
/// independiente con borde, mismo criterio visual que <see cref="BuyerSection"/> (Factura/Nota de
/// Crédito). Consume únicamente <c>SubjectWithheld</c>/<c>Header.FiscalPeriod</c> del layout — el
/// emisor ya se muestra en <see cref="RetentionHeaderSection"/>, esta sección nunca lo repite.
/// </summary>
public static class RetentionSubjectSection
{
    public static void Compose(IContainer container, RetentionRideDocumentLayout layout)
    {
        var subject = layout.SubjectWithheld;
        var header = layout.Header;

        container
            .RideBox()
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);
                column
                    .Item()
                    .Text($"Razón Social / Nombres y Apellidos: {subject.LegalName}")
                    .FontSize(9);
                column
                    .Item()
                    .Row(row =>
                    {
                        row.RelativeItem(2)
                            .Text($"Tipo Identificación: {subject.IdentificationType ?? string.Empty}")
                            .FontSize(9);
                        row.RelativeItem(3)
                            .Text($"Identificación: {subject.IdentificationNumber}")
                            .FontSize(9);
                    });
                column.Item().Text($"Período Fiscal: {header.FiscalPeriod}").FontSize(9);
            });
    }
}
