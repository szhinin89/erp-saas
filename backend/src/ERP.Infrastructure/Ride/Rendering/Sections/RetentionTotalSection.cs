using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — caja de Total Retenido, mismo criterio visual que la caja
/// de Totales de Factura (<see cref="TaxSummarySection"/>) pero sin desglose por tarifa de
/// IVA/ICE — ese desglose no existe en <c>comprobanteRetencion</c>. Consume únicamente
/// <c>TotalRetained</c> del layout (ya calculado por el parser como suma de las líneas del propio
/// XML, RETENTIONS-RIDE-TEMPLATE-03C) — nunca vuelve a sumar las líneas por su cuenta.
/// </summary>
public static class RetentionTotalSection
{
    public static void Compose(IContainer container, RetentionRideDocumentLayout layout)
    {
        container
            .RideBox()
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);
                column
                    .Item()
                    .Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL RETENIDO").Bold().FontSize(9);
                        r.ConstantItem(70)
                            .AlignRight()
                            .Text(layout.TotalRetained.ToString("F2", CultureInfo.InvariantCulture))
                            .Bold()
                            .FontSize(9);
                    });
            });
    }
}
