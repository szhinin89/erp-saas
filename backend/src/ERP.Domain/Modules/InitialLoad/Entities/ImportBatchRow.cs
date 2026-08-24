using ERP.Domain.Common;

namespace ERP.Domain.Modules.InitialLoad.Entities;

/// <summary>
/// Fila en staging de un <see cref="ImportBatch"/>. Agregado propio (ver nota de diseño en
/// <see cref="ImportBatch"/>), con su propio repositorio paginado — nunca cargado como colección
/// en memoria de la cabecera.
///
/// <see cref="RawData"/>/<see cref="ParsedData"/> son JSON (jsonb) genérico y reusable por
/// cualquier <c>ImportType</c> — el dominio nunca los interpreta; el parseo tipado (columna →
/// DTO) vive en el <c>IImportProcessor</c> de Application/Infrastructure. Este JSON es staging y
/// auditoría temporal, nunca sustituye los DTOs/validaciones tipadas del import type.
/// </summary>
public sealed class ImportBatchRow : AuditableEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public int RowNumber { get; private set; }
    public string RawData { get; private set; } = null!;
    public string? ParsedData { get; private set; }
    public bool HasBlockingIssue { get; private set; }
    public bool IsImported { get; private set; }
    public Guid? CreatedBusinessPartnerId { get; private set; }

    private ImportBatchRow() { }

    public static ImportBatchRow Create(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        int rowNumber,
        string rawDataJson,
        Guid createdBy
    )
    {
        if (rowNumber < 1)
            throw new ArgumentException("El número de fila debe ser mayor a 0.", nameof(rowNumber));
        if (string.IsNullOrWhiteSpace(rawDataJson))
            throw new ArgumentException("Los datos crudos de la fila son obligatorios.", nameof(rawDataJson));

        var row = new ImportBatchRow
        {
            TenantId = tenantId,
            CompanyId = companyId,
            ImportBatchId = importBatchId,
            RowNumber = rowNumber,
            RawData = rawDataJson,
        };
        row.SetCreated(createdBy);
        return row;
    }

    public void SetParsedData(string parsedDataJson, bool hasBlockingIssue, Guid updatedBy)
    {
        ParsedData = parsedDataJson;
        HasBlockingIssue = hasBlockingIssue;
        SetUpdated(updatedBy);
    }

    public void MarkImported(Guid createdBusinessPartnerId, Guid updatedBy)
    {
        if (HasBlockingIssue)
            throw new InvalidOperationException(
                "No se puede marcar como importada una fila con error bloqueante."
            );
        IsImported = true;
        CreatedBusinessPartnerId = createdBusinessPartnerId;
        SetUpdated(updatedBy);
    }
}
