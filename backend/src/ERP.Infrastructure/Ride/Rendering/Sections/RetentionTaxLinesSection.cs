using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — tabla de impuestos retenidos, con bordes por celda y
/// encabezado destacado, mismo criterio visual que <see cref="LinesSection"/>. Consume
/// únicamente <c>Lines</c> del layout — nunca recalcula bases, porcentajes ni valores.
/// </summary>
public static class RetentionTaxLinesSection
{
    public static void Compose(IContainer container, RetentionRideDocumentLayout layout)
    {
        if (layout.Lines.Count == 0)
        {
            container.RideBox().Padding(6).Text("Sin líneas de retención.").FontSize(8);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1f); // Impuesto
                columns.RelativeColumn(1.5f); // Código Retención
                columns.RelativeColumn(1.5f); // Base Imponible
                columns.RelativeColumn(1.2f); // % Retención
                columns.RelativeColumn(1.5f); // Valor Retenido
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Impuesto");
                header.Cell().Element(HeaderCell).Text("Cód. Retención");
                header.Cell().Element(HeaderCell).Text("Base Imponible");
                header.Cell().Element(HeaderCell).Text("% Retención");
                header.Cell().Element(HeaderCell).Text("Valor Retenido");
            });

            foreach (var line in layout.Lines)
            {
                table.Cell().Element(BodyCell).Text(line.TaxCode);
                table.Cell().Element(BodyCell).Text(line.RetentionCode);
                table
                    .Cell()
                    .Element(BodyCell)
                    .AlignRight()
                    .Text(line.BaseAmount.ToString("F2", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .Element(BodyCell)
                    .AlignRight()
                    .Text(line.RetentionRate.ToString("F2", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .Element(BodyCell)
                    .AlignRight()
                    .Text(line.RetainedAmount.ToString("F2", CultureInfo.InvariantCulture));
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(Colors.Grey.Lighten3)
            .RideBox()
            .Padding(2)
            .DefaultTextStyle(x => x.Bold().FontSize(7));

    private static IContainer BodyCell(IContainer container) =>
        container.RideBox().Padding(2).DefaultTextStyle(x => x.FontSize(8));
}
