using ERP.Domain.Common;

namespace ERP.Domain.Modules.InitialLoad.Entities;

/// <summary>
/// Archivo Excel subido para un <see cref="ImportBatch"/>. Colección hija pequeña (1-2 archivos
/// por lote — re-subir agrega un nuevo registro, nunca reemplaza el histórico), a diferencia de
/// <c>ImportBatchRow</c>/<c>ImportBatchIssue</c> que son agregados propios. <see cref="StoredPath"/>
/// es la ruta opaca devuelta por <c>IFileStorage.SaveAsync</c> — misma abstracción de storage que
/// usa el resto del backend, sin introducir un nuevo mecanismo.
/// </summary>
public sealed class ImportBatchFile : AuditableEntity, ITenantScopedEntity
{
    public const int FileNameMaxLen = 260;
    public const int StoredPathMaxLen = 500;

    public Guid ImportBatchId { get; private set; }
    public string StoredPath { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private ImportBatchFile() { }

    public static ImportBatchFile Create(
        Guid tenantId,
        Guid importBatchId,
        string storedPath,
        string fileName,
        long sizeBytes,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            throw new ArgumentException("La ruta del archivo es obligatoria.", nameof(storedPath));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El nombre del archivo es obligatorio.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentException("El archivo está vacío.", nameof(sizeBytes));

        var file = new ImportBatchFile
        {
            TenantId = tenantId,
            ImportBatchId = importBatchId,
            StoredPath = storedPath.Trim(),
            FileName = fileName.Trim(),
            SizeBytes = sizeBytes,
            UploadedAt = DateTime.UtcNow,
        };
        file.SetCreated(createdBy);
        return file;
    }
}
