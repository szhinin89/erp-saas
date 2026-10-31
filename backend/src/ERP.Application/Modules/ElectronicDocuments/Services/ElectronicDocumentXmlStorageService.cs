using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Único consumidor de <c>IFileStorage</c> para XML de ElectronicDocuments — no crea un
/// segundo sistema de almacenamiento, solo orquesta <see cref="IElectronicDocumentStorageNamingStrategy"/>
/// (nombre lógico) + <c>IFileStorage</c> (escritura real).
/// </summary>
public sealed partial class ElectronicDocumentXmlStorageService
    : IElectronicDocumentXmlStorageService
{
    private readonly IFileStorage _fileStorage;
    private readonly IElectronicDocumentStorageNamingStrategy _namingStrategy;
    private readonly ILogger<ElectronicDocumentXmlStorageService> _logger;

    public ElectronicDocumentXmlStorageService(
        IFileStorage fileStorage,
        IElectronicDocumentStorageNamingStrategy namingStrategy,
        ILogger<ElectronicDocumentXmlStorageService> logger
    )
    {
        _fileStorage = fileStorage;
        _namingStrategy = namingStrategy;
        _logger = logger;
    }

    public Task<Result<ElectronicDocumentStoredXmlPaths>> StoreAsync(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        ElectronicDocumentXml draftXml,
        SignedElectronicDocumentXml signedXml,
        CancellationToken ct = default
    ) =>
        StoreCoreAsync(
            electronicDocumentId,
            _namingStrategy.BuildRelativePath(
                tenantId,
                documentType,
                electronicDocumentId,
                ElectronicDocumentXmlVariant.Draft
            ),
            _namingStrategy.BuildRelativePath(
                tenantId,
                documentType,
                electronicDocumentId,
                ElectronicDocumentXmlVariant.Signed
            ),
            draftXml,
            signedXml,
            ct
        );

    public async Task<Result<ElectronicDocumentStoredXmlPaths>> StoreAttemptAsync(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        Guid attemptId,
        ElectronicDocumentXml draftXml,
        SignedElectronicDocumentXml signedXml,
        CancellationToken ct = default
    )
    {
        var draftRelativePath = _namingStrategy.BuildAttemptRelativePath(
            tenantId,
            documentType,
            electronicDocumentId,
            attemptId,
            ElectronicDocumentXmlVariant.Draft
        );
        var signedRelativePath = _namingStrategy.BuildAttemptRelativePath(
            tenantId,
            documentType,
            electronicDocumentId,
            attemptId,
            ElectronicDocumentXmlVariant.Signed
        );

        // Evidencia versionada: un intento nunca reutiliza una ruta existente.
        foreach (var path in new[] { draftRelativePath, signedRelativePath })
        {
            await using var existing = await _fileStorage.GetAsync(path, ct);
            if (existing is not null)
                return Result<ElectronicDocumentStoredXmlPaths>.Failure(
                    "Ya existe un XML almacenado para este intento de retransmisión; no se sobrescribe la evidencia."
                );
        }

        return await StoreCoreAsync(
            electronicDocumentId,
            draftRelativePath,
            signedRelativePath,
            draftXml,
            signedXml,
            ct
        );
    }

    public async Task DiscardAttemptAsync(
        ElectronicDocumentStoredXmlPaths paths,
        CancellationToken ct = default
    )
    {
        await _fileStorage.DeleteAsync(paths.DraftXmlPath, ct);
        await _fileStorage.DeleteAsync(paths.SignedXmlPath, ct);
    }

    public async Task<Result<byte[]>> ReadAsync(string storedPath, CancellationToken ct = default)
    {
        try
        {
            await using var stream = await _fileStorage.GetAsync(storedPath, ct);
            if (stream is null)
                return Result<byte[]>.NotFound("El XML almacenado no existe.");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return Result<byte[]>.Success(buffer.ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<byte[]>.Failure($"No se pudo leer el XML almacenado: {ex.Message}");
        }
    }

    private async Task<Result<ElectronicDocumentStoredXmlPaths>> StoreCoreAsync(
        Guid electronicDocumentId,
        string draftRelativePath,
        string signedRelativePath,
        ElectronicDocumentXml draftXml,
        SignedElectronicDocumentXml signedXml,
        CancellationToken ct
    )
    {
        string draftStoredPath;
        try
        {
            using var draftStream = new MemoryStream(Encoding.UTF8.GetBytes(draftXml.Xml));
            draftStoredPath = await _fileStorage.SaveAsync(draftRelativePath, draftStream, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(electronicDocumentId, "draft", ex);
            return Result<ElectronicDocumentStoredXmlPaths>.Failure(
                $"No se pudo almacenar el XML generado: {ex.Message}"
            );
        }

        try
        {
            using var signedStream = new MemoryStream(Encoding.UTF8.GetBytes(signedXml.SignedXml));
            var signedStoredPath = await _fileStorage.SaveAsync(
                signedRelativePath,
                signedStream,
                ct
            );

            return Result<ElectronicDocumentStoredXmlPaths>.Success(
                new ElectronicDocumentStoredXmlPaths(draftStoredPath, signedStoredPath)
            );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Compensación best-effort: el borrador ya se escribió pero el firmado falló — no
            // debe quedar un archivo huérfano sin ElectronicDocument que lo referencie. Si el
            // borrado también falla, se prioriza reportar el error original de almacenamiento.
            LogStorageFailed(electronicDocumentId, "signed", ex);
            await _fileStorage.DeleteAsync(draftStoredPath, ct);
            LogOrphanedDraftDeleted(electronicDocumentId, draftStoredPath);
            return Result<ElectronicDocumentStoredXmlPaths>.Failure(
                $"No se pudo almacenar el XML firmado: {ex.Message}"
            );
        }
    }

    public async Task<Result<string>> StoreAuthorizedAsync(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        string authorizedXml,
        CancellationToken ct = default
    )
    {
        try
        {
            var relativePath = _namingStrategy.BuildRelativePath(
                tenantId,
                documentType,
                electronicDocumentId,
                ElectronicDocumentXmlVariant.Authorized
            );
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(authorizedXml));
            var storedPath = await _fileStorage.SaveAsync(relativePath, stream, ct);
            return Result<string>.Success(storedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(electronicDocumentId, "authorized", ex);
            return Result<string>.Failure($"No se pudo almacenar el XML autorizado: {ex.Message}");
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[ElectronicDocuments] No se pudo almacenar el XML '{Variant}' del documento {ElectronicDocumentId}"
    )]
    private partial void LogStorageFailed(Guid electronicDocumentId, string variant, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[ElectronicDocuments] Borrador huérfano eliminado tras fallo de almacenamiento del firmado — documento {ElectronicDocumentId}, ruta {DraftPath}"
    )]
    private partial void LogOrphanedDraftDeleted(Guid electronicDocumentId, string draftPath);
}
