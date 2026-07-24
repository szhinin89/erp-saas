using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Campos adicionales del comprobante, en caja con borde — formato oficial del SRI
/// (<c>Nombre : Valor</c>, nunca texto corrido). Consume únicamente <c>AdditionalInfo</c> del layout.
///
/// La caja siempre se dibuja, incluso sin campos — antes se omitía por completo cuando
/// <c>AdditionalInfo</c> estaba vacío, dejando un hueco sin borde junto a la caja de Totales
/// (Fase 14: corregido tras comparar contra un comprobante real sin información adicional).
/// </summary>
public static class AdditionalInfoSection
{
    public static void Compose(IContainer container, InvoiceRideDocumentLayout layout)
    {
        container.RideBox().Column(column =>
        {
            column.Item().Background(Colors.Grey.Lighten3).Padding(4).Text("Información Adicional").Bold().FontSize(9);

            if (layout.AdditionalInfo.Count == 0)
                return;

            column.Item().Padding(6).Column(inner =>
            {
                inner.Spacing(2);
                foreach (var field in layout.AdditionalInfo)
                    inner.Item().Text($"{field.Name} : {field.Value}").FontSize(8);
            });
        });
    }
}
