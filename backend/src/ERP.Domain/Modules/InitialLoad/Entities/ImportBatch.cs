using ERP.Domain.Common;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Entities;

/// <summary>
/// Cabecera de un lote de Carga Inicial (INITIAL-LOAD-ARCH-01). Aggregate root company-scoped.
///
/// DECISIÓN DE AGREGADOS: <see cref="ImportBatchRow"/> e <see cref="ImportBatchIssue"/> NO son
/// colecciones hijas de este aggregate — son agregados propios con su propio repositorio,
/// relacionados por <c>ImportBatchId</c>. Un Excel puede tener miles de filas; Validate/Preview/
/// Confirm son operaciones fila-a-fila o paginadas por naturaleza — cargarlas todas en memoria
/// junto a la cabecera violaría el patrón de agregados pequeños ya usado en el resto del dominio
/// (ver <c>Item</c>, cuyas colecciones son acotadas). Este aggregate solo mantiene contadores,
/// actualizados explícitamente por los handlers tras procesar las filas.
/// </summary>
public sealed class ImportBatch : AuditableEntity, ICompanyOperationalEntity
{
    private readonly List<ImportBatchFile> _files = new();

    public Guid CompanyId { get; private set; }
    public ImportType ImportType { get; private set; }
    public ImportStatus Status { get; private set; }
    public string? Label { get; private set; }

    /// <summary>
    /// Modo opcional (INITIAL-LOAD-ITEMS-01 → Catálogo de Productos): cuando está activo, un
    /// <c>IImportProcessor</c> puede crear entradas de catálogo (p. ej. Categoría/Marca) que no
    /// existan aún, en vez de bloquear la fila. Default false — bloquear es el comportamiento
    /// seguro; el usuario debe optar explícitamente por la creación automática. Fijo desde
    /// <see cref="Create"/>, no editable después.
    /// </summary>
    public bool AutoCreateCatalogValues { get; private set; }

    public int TotalRows { get; private set; }
    public int ValidRows { get; private set; }
    public int IssueRows { get; private set; }
    public int WarningRows { get; private set; }
    public int ImportedRows { get; private set; }

    public DateTime? ValidatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? FailureReason { get; private set; }

    public IReadOnlyList<ImportBatchFile> Files => _files.AsReadOnly();

    private ImportBatch() { }

    public static ImportBatch Create(
        Guid tenantId,
        Guid companyId,
        ImportType importType,
        Guid createdBy,
        string? label = null,
        bool autoCreateCatalogValues = false
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));

        var batch = new ImportBatch
        {
            TenantId = tenantId,
            CompanyId = companyId,
            ImportType = importType,
            Status = ImportStatus.Draft,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            AutoCreateCatalogValues = autoCreateCatalogValues,
        };
        batch.SetCreated(createdBy);
        return batch;
    }

    public ImportBatchFile AttachFile(
        string storedPath,
        string fileName,
        long sizeBytes,
        Guid updatedBy
    )
    {
        EnsureStatus(
            "adjuntar un archivo",
            ImportStatus.Draft,
            ImportStatus.Uploaded
        );

        var file = ImportBatchFile.Create(TenantId, Id, storedPath, fileName, sizeBytes, updatedBy);
        _files.Add(file);
        SetUpdated(updatedBy);
        return file;
    }

    public void MarkUploaded(Guid updatedBy)
    {
        EnsureStatus("marcar como subido", ImportStatus.Draft, ImportStatus.Uploaded);
        if (_files.Count == 0)
            throw new InvalidOperationException("El lote no tiene ningún archivo adjunto.");
        Status = ImportStatus.Uploaded;
        SetUpdated(updatedBy);
    }

    public void BeginValidating(Guid updatedBy)
    {
        EnsureStatus("validar", ImportStatus.Uploaded, ImportStatus.Validated);
        Status = ImportStatus.Validating;
        SetUpdated(updatedBy);
    }

    public void CompleteValidation(
        int totalRows,
        int validRows,
        int issueRows,
        int warningRows,
        Guid updatedBy
    )
    {
        EnsureStatus("completar la validación", ImportStatus.Validating);
        TotalRows = totalRows;
        ValidRows = validRows;
        IssueRows = issueRows;
        WarningRows = warningRows;
        Status = ImportStatus.Validated;
        ValidatedAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void BeginConfirming(Guid updatedBy)
    {
        EnsureStatus("confirmar", ImportStatus.Validated);
        Status = ImportStatus.Confirming;
        SetUpdated(updatedBy);
    }

    public void CompleteConfirmation(int importedRows, bool anyRowsFailed, Guid updatedBy)
    {
        EnsureStatus("completar la confirmación", ImportStatus.Confirming);
        ImportedRows = importedRows;
        Status =
            !anyRowsFailed && importedRows == ValidRows
                ? ImportStatus.Completed
                : ImportStatus.PartiallyCompleted;
        ConfirmedAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void Fail(string reason, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de la falla es obligatorio.", nameof(reason));
        Status = ImportStatus.Failed;
        FailureReason = reason.Trim();
        SetUpdated(updatedBy);
    }

    public void Cancel(Guid updatedBy)
    {
        EnsureStatus(
            "cancelar",
            ImportStatus.Draft,
            ImportStatus.Uploaded,
            ImportStatus.Validated
        );
        Status = ImportStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    private void EnsureStatus(string action, params ImportStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new InvalidOperationException(
                $"No se puede {action} un lote en estado '{Status}'."
            );
    }
}
