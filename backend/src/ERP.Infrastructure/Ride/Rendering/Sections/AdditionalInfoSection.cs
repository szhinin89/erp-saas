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
    public static void Compose(IContainer container, InvoiceRideDocumentLayout layout) =>
        Compose(container, layout.AdditionalInfo);

    /// <summary>RETENTIONS-RIDE-PDF-RENDERER-03D — mismo render, para el Comprobante de Retención
    /// (<see cref="RetentionRideDocumentLayout"/> también expone <c>AdditionalInfo</c>, misma
    /// forma exacta) — evita duplicar esta sección para un segundo tipo de comprobante.</summary>
    public static void Compose(IContainer container, RetentionRideDocumentLayout layout) =>
        Compose(container, layout.AdditionalInfo);

    private static void Compose(
        IContainer container,
        IReadOnlyList<ERP.Domain.Modules.Ride.ValueObjects.RideAdditionalInfo> additionalInfo
    )
    {
        container
            .RideBox()
            .Column(column =>
            {
                column
                    .Item()
                    .Background(Colors.Grey.Lighten3)
                    .Padding(4)
                    .Text("Información Adicional")
                    .Bold()
                    .FontSize(9);

                if (additionalInfo.Count == 0)
                    return;

                column
                    .Item()
                    .Padding(6)
                    .Column(inner =>
                    {
                        inner.Spacing(2);
                        foreach (var field in additionalInfo)
                            inner.Item().Text($"{field.Name} : {field.Value}").FontSize(8);
                    });
            });
    }
}
