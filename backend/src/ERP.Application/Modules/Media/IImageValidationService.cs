using ERP.Application.Common;
using ERP.Application.Common.Models;

namespace ERP.Application.Modules.Media;

/// <summary>
/// Valida que un contenido subido sea una imagen soportada (PNG/JPEG/WEBP),
/// dentro de los límites de tamaño y dimensiones, inspeccionando el contenido
/// real del archivo (magic bytes) en lugar del Content-Type declarado.
/// </summary>
public interface IImageValidationService
{
    Result<ImageMetadata> Validate(MediaUploadContent content);
}
