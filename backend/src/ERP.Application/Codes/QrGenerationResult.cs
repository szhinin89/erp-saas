namespace ERP.Application.Codes;

/// <summary>
/// Resultado genérico de una generación de código QR: la imagen PNG ya codificada como bytes.
/// </summary>
public sealed class QrGenerationResult
{
    public byte[] PngBytes { get; }

    public QrGenerationResult(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        PngBytes = pngBytes;
    }
}
