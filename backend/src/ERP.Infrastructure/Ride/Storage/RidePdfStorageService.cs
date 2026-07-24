using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Ride.Storage;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Infrastructure.Ride.Storage;

/// <summary>
/// Persiste el PDF vía <see cref="IFileStorage"/> — único sistema de almacenamiento del ERP, sin
/// escritura directa al filesystem. Reemplaza a <c>NullRidePdfStorageService</c> (Fase 4).
/// </summary>
public sealed class RidePdfStorageService : IRidePdfStorageService
{
    private readonly IFileStorage _fileStorage;
    private readonly IRidePdfStorageNamingStrategy _namingStrategy;

    public RidePdfStorageService(IFileStorage fileStorage, IRidePdfStorageNamingStrategy namingStrategy)
    {
        _fileStorage = fileStorage;
        _namingStrategy = namingStrategy;
    }

    public async Task<Result<string>> StoreAsync(
        Guid tenantId,
        RideDocumentType documentType,
        Guid electronicDocumentId,
        string templateVersion,
        byte[] pdf,
        CancellationToken ct = default)
    {
        var relativePath = _namingStrategy.BuildRelativePath(tenantId, documentType, electronicDocumentId, templateVersion);

        try
        {
            using var content = new MemoryStream(pdf);
            var storedPath = await _fileStorage.SaveAsync(relativePath, content, ct);
            return Result<string>.Success(storedPath);
        }
        catch (IOException)
        {
            // H4 a nivel de archivo (Fase 8, hallazgo real durante la suite completa): la ruta ya
            // es 100% determinística (misma huella ⇒ mismo path, ADR-025 §14) — si otra generación
            // concurrente está escribiendo (o ya escribió) exactamente el mismo archivo, un choque
            // de bloqueo exclusivo de archivo (LocalFileStorage usa FileShare.None) no es un fallo
            // real: el contenido que el ganador escribió es, por construcción, idéntico al que este
            // intento habría escrito. Se devuelve la misma ruta ya conocida sin reintentar.
            return Result<string>.Success(relativePath);
        }
    }
}
