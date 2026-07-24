namespace ERP.Application.Modules.Media;

/// <summary>Metadatos detectados de una imagen a partir de su contenido real (magic bytes).</summary>
public sealed record ImageMetadata(string ContentType, string Extension, int Width, int Height);
