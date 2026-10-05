using ERP.Application.Common.Models;

namespace ERP.API.Uploads;

/// <summary>
/// ZH-API-THIN-MEDIA-UPLOAD-01 — única conversión de capa API <see cref="IFormFile"/> →
/// <see cref="MediaUploadContent"/> (antes copiada en 5 endpoints multipart): copia el archivo a un
/// buffer en memoria, lo rebobina y conserva FileName, ContentType y Length tal cual llegan.
/// No valida nada: el 400 por archivo ausente/vacío es propio de cada endpoint y los límites de
/// tipo/tamaño viven en los validators de Application.
/// Lifecycle: el buffer pertenece a quien llama a <see cref="CreateAsync"/>, que lo libera con
/// <c>await using</c> DESPUÉS del <c>mediator.Send</c> (los handlers consumen el stream dentro del Send).
/// </summary>
public sealed class BufferedFormFile : IAsyncDisposable
{
    private readonly MemoryStream _buffer;

    private BufferedFormFile(MemoryStream buffer, MediaUploadContent content)
    {
        _buffer = buffer;
        Content = content;
    }

    /// <summary>Contenido listo para el command de Application (stream en posición 0).</summary>
    public MediaUploadContent Content { get; }

    public static async Task<BufferedFormFile> CreateAsync(
        IFormFile file,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(file);

        var buffer = new MemoryStream();
        try
        {
            await file.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return new BufferedFormFile(
                buffer,
                new MediaUploadContent(buffer, file.FileName, file.ContentType, file.Length)
            );
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _buffer.DisposeAsync();
}
