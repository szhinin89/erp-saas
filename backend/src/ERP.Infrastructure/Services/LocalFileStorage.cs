using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Almacenamiento de archivos en el sistema de ficheros local.
/// Configuración en appsettings.json: <c>FileStorage:BasePath</c>.
/// Si el path no está configurado, usa la subcarpeta <c>files</c> dentro del directorio de trabajo.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IConfiguration configuration)
    {
        var configured = configuration["FileStorage:BasePath"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "files")
            : configured;
    }

    public async Task<string> SaveAsync(
        string relativePath,
        Stream content,
        CancellationToken cancellationToken = default
    )
    {
        var fullPath = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fs = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            65536,
            useAsync: true
        );
        await content.CopyToAsync(fs, cancellationToken);

        return relativePath;
    }

    public Task<Stream?> GetAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = FullPath(storedPath);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65536,
            useAsync: true
        );
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = FullPath(storedPath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string FullPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(_basePath, relativePath));
}
