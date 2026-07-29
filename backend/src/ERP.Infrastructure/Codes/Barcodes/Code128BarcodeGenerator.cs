using ERP.Application.Codes.Barcodes;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace ERP.Infrastructure.Codes.Barcodes;

/// <summary>
/// Implementación concreta de <see cref="IBarcodeGenerator"/> para Code128, sobre ZXing.Net +
/// SkiaSharp (mismo criterio cross-platform que <c>QrCodeGenerator</c>: sin System.Drawing, apto
/// para contenedores Linux). <see cref="ZXing.BarcodeWriterPixelData"/> produce el bitmap crudo
/// en memoria (formato BGRA32), que se envuelve en un <see cref="SKBitmap"/> únicamente para
/// codificarlo a PNG — sin dependencia de ningún motor de render adicional. Sin estado,
/// reutilizable por cualquier módulo del ERP. Otras simbologías (Code39, EAN13, UPC, PDF417,
/// DataMatrix) se agregan como una nueva clase que implemente <see cref="IBarcodeGenerator"/>,
/// nunca modificando esta (OCP) — ver <see cref="BarcodeSymbology"/>.
/// </summary>
public sealed partial class Code128BarcodeGenerator : IBarcodeGenerator
{
    private readonly ILogger<Code128BarcodeGenerator> _logger;

    public Code128BarcodeGenerator(ILogger<Code128BarcodeGenerator> logger)
    {
        _logger = logger;
    }

    public BarcodeGenerationResult Generate(BarcodeGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Symbology != BarcodeSymbology.Code128)
            throw new NotSupportedException(
                $"Code128BarcodeGenerator solo soporta {nameof(BarcodeSymbology.Code128)} — simbología recibida: {request.Symbology}."
            );

        try
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = request.Width,
                    Height = request.Height,
                    Margin = 5,
                    PureBarcode = true,
                },
            };

            var pixelData = writer.Write(request.Content);
            var pngBytes = ToPng(pixelData);
            return new BarcodeGenerationResult(pngBytes);
        }
        catch (Exception ex)
        {
            // Solo diagnóstico técnico — nunca el contenido codificado, mismo criterio que
            // QrCodeGenerator. La excepción se re-lanza sin alterar el comportamiento.
            LogGenerationFailed(ex, request.Content.Length, request.Symbology);
            throw;
        }
    }

    private static byte[] ToPng(PixelData pixelData)
    {
        var info = new SKImageInfo(
            pixelData.Width,
            pixelData.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );
        using var bitmap = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(
            pixelData.Pixels,
            0,
            bitmap.GetPixels(),
            pixelData.Pixels.Length
        );
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Fallo inesperado generando un código de barras (longitud de contenido: {ContentLength}, simbología: {Symbology})."
    )]
    private partial void LogGenerationFailed(
        Exception ex,
        int contentLength,
        BarcodeSymbology symbology
    );
}
