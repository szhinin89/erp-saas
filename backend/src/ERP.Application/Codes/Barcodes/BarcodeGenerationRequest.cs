namespace ERP.Application.Codes.Barcodes;

/// <summary>
/// Solicitud genérica de generación de un código de barras 1D. Sin conocimiento de ningún
/// dominio consumidor (Ride, Inventario, Activos, POS, etc.) — únicamente el contenido a
/// codificar, la simbología y el tamaño de renderizado.
/// </summary>
public sealed class BarcodeGenerationRequest
{
    /// <summary>Techo defensivo de tamaño — un código de barras legible no necesita más resolución que esto.</summary>
    public const int MaxWidth = 2000;
    public const int MaxHeight = 500;

    public string Content { get; }
    public BarcodeSymbology Symbology { get; }
    public int Width { get; }
    public int Height { get; }

    public BarcodeGenerationRequest(
        string content, BarcodeSymbology symbology = BarcodeSymbology.Code128, int width = 600, int height = 100)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("El contenido a codificar es obligatorio.", nameof(content));
        if (width <= 0 || width > MaxWidth)
            throw new ArgumentException($"El ancho debe estar entre 1 y {MaxWidth}.", nameof(width));
        if (height <= 0 || height > MaxHeight)
            throw new ArgumentException($"El alto debe estar entre 1 y {MaxHeight}.", nameof(height));

        Content = content;
        Symbology = symbology;
        Width = width;
        Height = height;
    }
}
