using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Ride.Barcodes;
using ERP.Application.Modules.Ride.Qr;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Templates;
using ERP.Infrastructure.Ride.Rendering.Sections;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace ERP.Infrastructure.Ride.Rendering;

/// <summary>
/// Única implementación de <see cref="IRideRenderer"/> (ADR-025 §13). Transformación pura:
/// <see cref="IRideDocumentLayout"/> → PDF. Nunca vuelve a interpretar XML, nunca consulta
/// <c>RideModel</c> directamente ni ningún repositorio — todo dato visual proviene exclusivamente
/// de las propiedades ya compuestas del layout recibido. Dos excepciones puntuales de I/O, ambas
/// resueltas antes de componer el documento: la generación visual del QR, delegada a
/// <see cref="IRideQrCodeGenerator"/> (contrato interno de Ride, ADR-025 §8), y la lectura de los
/// bytes del logo de marca vía <see cref="IFileStorage"/> (interfaz ya existente, sin
/// modificarla) — nunca implementadas aquí.
///
/// Composición de página clonada del PDF de referencia (Fase 14): encabezado en tres cajas
/// (logo, datos del emisor, datos del comprobante), comprador en caja propia, detalle en tabla
/// con bordes, y una fila final con información adicional + formas de pago apiladas a la
/// izquierda junto a la caja de Totales a la derecha (sin caja de Impuestos separada — no existe
/// en la referencia), QR + clave de acceso en caja, y el texto legal
/// "Representación impresa del comprobante electrónico" en el pie — mismo <c>RideModel</c>,
/// mismo pipeline, solo cambia la presentación.
///
/// Solo soporta <see cref="InvoiceRideDocumentLayout"/> hoy porque es la única forma de layout
/// que existe (Fase 6) — el chequeo de tipo es sobre la FORMA de los datos ya compuestos, nunca
/// sobre <c>RideDocumentType</c> ni un <c>switch</c> documental. Un layout no soportado produce
/// una excepción que <c>RidePipeline</c> ya captura (Fase 5) y convierte en
/// <c>RideOutcome.Failed</c> — nunca se propaga sin control.
/// </summary>
public sealed class QuestPdfRideRenderer : IRideRenderer
{
    private readonly IRideQrCodeGenerator _qrCodeGenerator;
    private readonly IRideBarcodeGenerator _barcodeGenerator;
    private readonly IFileStorage _fileStorage;

    public QuestPdfRideRenderer(
        IRideQrCodeGenerator qrCodeGenerator, IRideBarcodeGenerator barcodeGenerator, IFileStorage fileStorage)
    {
        _qrCodeGenerator = qrCodeGenerator;
        _barcodeGenerator = barcodeGenerator;
        _fileStorage = fileStorage;
    }

    public async Task<byte[]> RenderAsync(IRideDocumentLayout layout, CancellationToken ct = default)
    {
        if (layout is not InvoiceRideDocumentLayout invoiceLayout)
            throw new NotSupportedException(
                $"QuestPdfRideRenderer no soporta todavía el tipo de layout '{layout.GetType().Name}'.");

        var qrImageBytes = _qrCodeGenerator.Generate(invoiceLayout.Header.AccessKey);
        var barcodeImageBytes = _barcodeGenerator.Generate(invoiceLayout.Header.AccessKey);
        var logoBytes = await ResolveLogoBytesAsync(invoiceLayout, ct);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().Element(c => HeaderSection.Compose(c, invoiceLayout, logoBytes, barcodeImageBytes));

                page.Content().PaddingVertical(6).Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Element(c => BuyerSection.Compose(c, invoiceLayout));
                    column.Item().Element(c => LinesSection.Compose(c, invoiceLayout));
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(3).Column(left =>
                        {
                            left.Spacing(6);
                            left.Item().Element(c => AdditionalInfoSection.Compose(c, invoiceLayout));
                            left.Item().Element(c => PaymentsSection.Compose(c, invoiceLayout));
                        });
                        row.ConstantItem(8);
                        row.RelativeItem(2).Element(c => TaxSummarySection.Compose(c, invoiceLayout));
                    });
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().Element(c => QrFooterSection.Compose(c, invoiceLayout, qrImageBytes));
                    footer.Item().PaddingTop(4).AlignCenter()
                        .Text("Representación impresa del comprobante electrónico.").FontSize(7);
                    footer.Item().AlignCenter().Text("www.sri.gob.ec").FontSize(7);
                });
            });
        });

        return document.GeneratePdf();
    }

    private async Task<byte[]?> ResolveLogoBytesAsync(InvoiceRideDocumentLayout layout, CancellationToken ct)
    {
        if (layout.Branding.LogoStoragePath is not { } path)
            return null;

        var stream = await _fileStorage.GetAsync(path, ct);
        if (stream is null)
            return null;

        await using (stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, ct);
            return memoryStream.ToArray();
        }
    }
}
