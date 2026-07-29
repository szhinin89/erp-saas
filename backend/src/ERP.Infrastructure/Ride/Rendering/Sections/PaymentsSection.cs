using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Formas de pago declaradas, en tabla con bordes — formato oficial del SRI. Consume
/// únicamente <c>Payments</c> del layout. Las columnas "Plazo"/"Unidad" solo se agregan cuando
/// al menos un pago realmente las trae (crédito) — el formato oficial muestra únicamente
/// "Forma de pago"/"Valor" para pagos de contado, sin columnas vacías de relleno.
/// </summary>
public static class PaymentsSection
{
    public static void Compose(IContainer container, InvoiceRideDocumentLayout layout)
    {
        if (layout.Payments.Count == 0)
        {
            container.RideBox().Padding(6).Text("Sin formas de pago registradas.").FontSize(8);
            return;
        }

        var hasCreditTerms = layout.Payments.Any(p => p.Term.HasValue || p.TimeUnit is not null);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
                if (hasCreditTerms)
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                }
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Forma de pago");
                header.Cell().Element(HeaderCell).Text("Valor");
                if (hasCreditTerms)
                {
                    header.Cell().Element(HeaderCell).Text("Plazo");
                    header.Cell().Element(HeaderCell).Text("Unidad");
                }
            });

            foreach (var payment in layout.Payments)
            {
                table.Cell().Element(BodyCell).Text(payment.PaymentMethodCode);
                table.Cell().Element(BodyCell).AlignRight().Text(payment.Amount.ToString("F2", CultureInfo.InvariantCulture));
                if (hasCreditTerms)
                {
                    table.Cell().Element(BodyCell).AlignRight().Text(payment.Term?.ToString(CultureInfo.InvariantCulture) ?? "-");
                    table.Cell().Element(BodyCell).Text(payment.TimeUnit ?? "-");
                }
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).RideBox()
            .Padding(3).DefaultTextStyle(x => x.Bold().FontSize(8));

    private static IContainer BodyCell(IContainer container) =>
        container.RideBox().Padding(3).DefaultTextStyle(x => x.FontSize(8));
}
