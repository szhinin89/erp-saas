using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Datos del comprador — caja independiente con borde, formato oficial del SRI. Consume
/// únicamente <c>Receiver</c>/<c>Header</c> del layout (el emisor ya se muestra en
/// <see cref="HeaderSection"/>, esta sección nunca lo repite).
///
/// "Placa / Matrícula" y "Guía Remisión" se muestran siempre como etiqueta, sin valor —
/// son campos estructurales del formato oficial (solo aplican a combustibles/traslado de
/// mercadería) que <c>RideModel</c> no modela porque el XML de factura estándar no los trae.
/// Mostrar la etiqueta en blanco replica la apariencia oficial sin inventar ningún dato.
/// </summary>
public static class BuyerSection
{
    public static void Compose(IContainer container, InvoiceRideDocumentLayout layout)
    {
        var receiver = layout.Receiver;
        var header = layout.Header;

        container
            .RideBox()
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);
                column
                    .Item()
                    .Text($"Razón Social / Nombres y Apellidos: {receiver.LegalName}")
                    .FontSize(9);
                column.Item().Text($"Identificación: {receiver.IdentificationNumber}").FontSize(9);

                column
                    .Item()
                    .Row(row =>
                    {
                        row.RelativeItem(2)
                            .Text($"Fecha: {header.IssueDate:dd/MM/yyyy}")
                            .FontSize(9);
                        row.RelativeItem(2).Text("Placa / Matrícula: ").FontSize(9);
                        row.RelativeItem(1).Text("Guía: ").FontSize(9);
                    });

                column.Item().Text($"Dirección: {receiver.Address ?? string.Empty}").FontSize(9);
            });
    }
}
