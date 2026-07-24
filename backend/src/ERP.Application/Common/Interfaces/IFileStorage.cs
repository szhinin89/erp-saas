namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Almacenamiento de archivos binarios (local o nube).
/// La ruta devuelta por <see cref="SaveAsync"/> es opaca — solo sirve para
/// pasarla de vuelta a <see cref="GetAsync"/> y <see cref="DeleteAsync"/>.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persiste el contenido y retorna la ruta relativa del archivo guardado.
    /// </summary>
    Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Abre el archivo almacenado. Retorna null si no existe.</summary>
    Task<Stream?> GetAsync(string storedPath, CancellationToken cancellationToken = default);

    /// <summary>Elimina el archivo. No lanza error si no existe.</summary>
    Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
}
