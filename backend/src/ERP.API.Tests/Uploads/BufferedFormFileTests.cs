using ERP.API.Uploads;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace ERP.API.Tests.Uploads;

/// <summary>
/// ZH-API-THIN-MEDIA-UPLOAD-01 — lifecycle de <see cref="BufferedFormFile"/>: el buffer queda
/// disponible hasta que el dueño lo libera, y si la copia falla no queda abierto (la excepción se
/// propaga como antes, cuando el MemoryStream del controller se liberaba al salir del método).
/// </summary>
public sealed class BufferedFormFileTests
{
    private sealed class FailingFormFile : IFormFile
    {
        public Stream? Target { get; private set; }

        public string ContentType => "application/x-test";
        public string ContentDisposition => "form-data";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => 10;
        public string Name => "file";
        public string FileName => "roto.bin";

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            Target = target;
            await target.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
            throw new IOException("conexión cortada a mitad del upload");
        }

        public void CopyTo(Stream target) => throw new NotSupportedException();
        public Stream OpenReadStream() => throw new NotSupportedException();
    }

    [Fact]
    public async Task Buffer_disponible_hasta_DisposeAsync_y_liberado_despues()
    {
        byte[] bytes = [9, 8, 7, 6];
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "a.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/x-test",
        };

        var upload = await BufferedFormFile.CreateAsync(file, default);

        upload.Content.Content.CanRead.Should().BeTrue();
        upload.Content.Content.Position.Should().Be(0);
        (upload.Content.FileName, upload.Content.ContentType, upload.Content.SizeBytes)
            .Should().Be(("a.bin", "application/x-test", 4L));

        await upload.DisposeAsync();

        upload.Content.Content.CanRead.Should().BeFalse();
    }

    [Fact]
    public async Task Copia_fallida_propaga_la_excepcion_y_no_deja_el_buffer_abierto()
    {
        var file = new FailingFormFile();

        await FluentActions.Invoking(() => BufferedFormFile.CreateAsync(file, default))
            .Should().ThrowAsync<IOException>();

        file.Target.Should().NotBeNull();
        file.Target!.CanRead.Should().BeFalse();
    }

    [Fact]
    public async Task Archivo_null_es_un_error_de_programacion_los_endpoints_validan_antes()
    {
        await FluentActions.Invoking(() => BufferedFormFile.CreateAsync(null!, default))
            .Should().ThrowAsync<ArgumentNullException>();
    }
}
