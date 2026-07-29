using ERP.Application.Codes;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace ERP.Infrastructure.Codes;

/// <summary>
/// Implementación concreta de <see cref="IQrCodeGenerator"/> sobre QRCoder. Usa
/// <see cref="PngByteQRCode"/> (encoder PNG puramente administrado, sin dependencia de
/// System.Drawing) para producir directamente los bytes de la imagen — apto para contenedores
/// Linux sin `libgdiplus`. Sin estado, reutilizable por cualquier módulo del ERP.
/// </summary>
public sealed partial class QrCodeGenerator : IQrCodeGenerator
{
    private readonly ILogger<QrCodeGenerator> _logger;

    public QrCodeGenerator(ILogger<QrCodeGenerator> logger)
    {
        _logger = logger;
    }

    public QrGenerationResult Generate(QrGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var qrCodeGenerator = new QRCodeGenerator();
            using var qrCodeData = qrCodeGenerator.CreateQrCode(
                request.Content,
                QRCodeGenerator.ECCLevel.M
            );
            using var pngQrCode = new PngByteQRCode(qrCodeData);

            var pngBytes = pngQrCode.GetGraphic(request.PixelsPerModule);
            return new QrGenerationResult(pngBytes);
        }
        catch (Exception ex)
        {
            // Solo diagnóstico técnico — nunca el contenido codificado (puede ser un dato de
            // negocio de cualquier módulo consumidor). La excepción se re-lanza sin alterar el
            // comportamiento: quien ya maneja errores del generador (p. ej. RidePipeline) sigue
            // recibiéndola igual.
            LogGenerationFailed(ex, request.Content.Length, request.PixelsPerModule);
            throw;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Fallo inesperado generando un código QR (longitud de contenido: {ContentLength}, píxeles por módulo: {PixelsPerModule})."
    )]
    private partial void LogGenerationFailed(Exception ex, int contentLength, int pixelsPerModule);
}
