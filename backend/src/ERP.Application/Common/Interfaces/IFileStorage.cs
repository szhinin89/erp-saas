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
    /// <param name="relativePath">Ruta relativa sugerida (p.ej. "compras/xml/2024/salesBill.xml").</param>
    /// <param name="content">Stream con el contenido a guardar.</param>
    Task<string> SaveAsync(string relativePath, Stream content, CancellationToken ct = default);

    /// <summary>Abre el archivo almacenado. Retorna null si no existe.</summary>
    Task<Stream?> GetAsync(string storedPath, CancellationToken ct = default);

    /// <summary>Elimina el archivo. No lanza error si no existe.</summary>
    Task DeleteAsync(string storedPath, CancellationToken ct = default);
}
