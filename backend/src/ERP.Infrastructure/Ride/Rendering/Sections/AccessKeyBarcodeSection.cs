using ERP.Domain.Modules.Ride.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>Presentación compartida; conserva el PNG Code128 y su relación de aspecto.</summary>
public static class AccessKeyBarcodeSection
{
    public static void Compose(IContainer container, RideAccessKey accessKey, byte[] barcodeImageBytes)
    {
        container.PaddingTop(4).Column(column =>
        {
            column.Item().AlignCenter().Text("CLAVE DE ACCESO").Bold().FontSize(8);
            // Márgenes adicionales a los de la caja, sin estirar ni recortar las barras.
            column.Item().PaddingTop(4).PaddingHorizontal(8)
                .AlignCenter().Image(barcodeImageBytes).FitWidth().UseOriginalImage();
            column.Item().PaddingTop(3).AlignCenter().Text(accessKey.Value).FontSize(7);
        });
    }
}
