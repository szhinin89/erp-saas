using System.Globalization;
using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Tabla de líneas de detalle, con bordes por celda y encabezado destacado — formato oficial
/// del SRI. Consume únicamente <c>Lines</c> del layout — nunca recalcula subtotales.
///
/// "Cód. Auxiliar", "Detalle Adicional", "Subsidio" y "Precio sin Subsidio" son columnas
/// estructurales del formato oficial que <c>RideLine</c> no modela (el XML de factura estándar
/// no las trae) — se muestran siempre vacías, replicando la apariencia oficial sin inventar
/// ningún valor.
/// </summary>
public static class LinesSection
{
    public static void Compose(IContainer container, InvoiceRideDocumentLayout layout)
    {
        if (layout.Lines.Count == 0)
        {
            container.RideBox().Padding(6).Text("Sin líneas de detalle.").FontSize(8);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.3f); // Cód. Principal
                columns.RelativeColumn(1.3f); // Cód. Auxiliar
                columns.RelativeColumn(0.8f); // Cantidad
                columns.RelativeColumn(3f);   // Descripción
                columns.RelativeColumn(1.5f); // Detalle Adicional
                columns.RelativeColumn(1.2f); // Precio Unitario
                columns.RelativeColumn(1f);   // Subsidio
                columns.RelativeColumn(1.2f); // Precio sin Subsidio
                columns.RelativeColumn(1f);   // Descuento
                columns.RelativeColumn(1.2f); // Precio Total
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Cod. Principal");
                header.Cell().Element(HeaderCell).Text("Cod. Auxiliar");
                header.Cell().Element(HeaderCell).Text("Cantidad");
                header.Cell().Element(HeaderCell).Text("Descripción");
                header.Cell().Element(HeaderCell).Text("Detalle Adicional");
                header.Cell().Element(HeaderCell).Text("Precio Unitario");
                header.Cell().Element(HeaderCell).Text("Subsidio");
                header.Cell().Element(HeaderCell).Text("Precio sin Subsidio");
                header.Cell().Element(HeaderCell).Text("Descuento");
                header.Cell().Element(HeaderCell).Text("Precio Total");
            });

            foreach (var line in layout.Lines)
            {
                table.Cell().Element(BodyCell).Text(line.Code);
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(line.Description);
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).AlignRight().Text(line.UnitPrice.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).AlignRight().Text(line.Discount.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).AlignRight().Text(line.Subtotal.ToString("F2", CultureInfo.InvariantCulture));
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).RideBox()
            .Padding(2).DefaultTextStyle(x => x.Bold().FontSize(6.5f));

    private static IContainer BodyCell(IContainer container) =>
        container.RideBox().Padding(2).DefaultTextStyle(x => x.FontSize(7));
}
