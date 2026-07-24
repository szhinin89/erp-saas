namespace ERP.Application.Codes.Barcodes;

/// <summary>Resultado genérico de una generación de código de barras: la imagen PNG ya codificada como bytes.</summary>
public sealed class BarcodeGenerationResult
{
    public byte[] PngBytes { get; }

    public BarcodeGenerationResult(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        PngBytes = pngBytes;
    }
}
