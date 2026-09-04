using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — documento sustento (comprobante del proveedor), caja
/// independiente con borde. Consume únicamente <c>SourceDocument</c> del layout —
/// <see cref="ERP.Domain.Modules.Ride.ValueObjects.RetentionRideSourceDocument"/> ya expone todos
/// sus campos como opcionales (ver RETENTIONS-RIDE-TEMPLATE-03C: el esquema
/// <c>ComprobanteRetencion_V1.0.0.xsd</c> los declara <c>minOccurs="0"</c>) — se muestran vacíos
/// cuando el XML no los trae, nunca inventados.
/// </summary>
public static class RetentionSourceDocumentSection
{
    public static void Compose(IContainer container, RetentionRideDocumentLayout layout)
    {
        var sourceDocument = layout.SourceDocument;

        container
            .RideBox()
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);
                column
                    .Item()
                    .Text("Documento Sustento")
                    .Bold()
                    .FontSize(9);
                column
                    .Item()
                    .Row(row =>
                    {
                        row.RelativeItem(2)
                            .Text($"Tipo/Código: {sourceDocument.DocumentTypeCode ?? string.Empty}")
                            .FontSize(9);
                        row.RelativeItem(3)
                            .Text($"Número: {sourceDocument.Number ?? string.Empty}")
                            .FontSize(9);
                        row.RelativeItem(2)
                            .Text(
                                $"Fecha Emisión: {(sourceDocument.IssueDate is { } date ? date.ToString("dd/MM/yyyy") : string.Empty)}"
                            )
                            .FontSize(9);
                    });
            });
    }
}
