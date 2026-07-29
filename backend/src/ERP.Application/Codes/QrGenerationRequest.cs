namespace ERP.Application.Codes;

/// <summary>
/// Solicitud genérica de generación de un código QR. Sin conocimiento de ningún dominio
/// consumidor (Ride, Inventory, Assets, POS, etc.) — únicamente el contenido a codificar y
/// el tamaño de renderizado.
/// </summary>
public sealed class QrGenerationRequest
{
    /// <summary>
    /// Techo real del estándar QR: capacidad máxima en modo byte de la Versión 40 (la más grande
    /// definida por la especificación ISO/IEC 18004) bajo ECC L (el nivel de corrección de
    /// errores más permisivo, el que más contenido admite). Ningún QR válido puede codificar más
    /// que esto sin importar la implementación — no es un límite arbitrario, es la pared física
    /// del formato. Se valida aquí (Application, agnóstico de QRCoder) para fallar rápido con un
    /// mensaje claro en vez de dejar que cada implementación concreta falle de forma distinta.
    /// </summary>
    public const int MaxContentLength = 2953;

    /// <summary>
    /// Techo defensivo de escala. Un QR de la Versión 40 tiene 177×177 módulos; a 100px/módulo ya
    /// produce un lienzo de ~17700×17700px — muy por encima de cualquier necesidad real de
    /// impresión o pantalla — por lo que valores mayores solo aportan riesgo de asignación de
    /// memoria desproporcionada sin ningún beneficio visual.
    /// </summary>
    public const int MaxPixelsPerModule = 100;

    public string Content { get; }
    public int PixelsPerModule { get; }

    public QrGenerationRequest(string content, int pixelsPerModule = 20)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "El contenido a codificar es obligatorio.",
                nameof(content)
            );
        if (content.Length > MaxContentLength)
            throw new ArgumentException(
                $"El contenido a codificar excede la capacidad máxima de un código QR ({MaxContentLength} caracteres).",
                nameof(content)
            );
        if (pixelsPerModule <= 0)
            throw new ArgumentException(
                "Los píxeles por módulo deben ser mayores a cero.",
                nameof(pixelsPerModule)
            );
        if (pixelsPerModule > MaxPixelsPerModule)
            throw new ArgumentException(
                $"Los píxeles por módulo no pueden exceder {MaxPixelsPerModule}.",
                nameof(pixelsPerModule)
            );

        Content = content;
        PixelsPerModule = pixelsPerModule;
    }
}
