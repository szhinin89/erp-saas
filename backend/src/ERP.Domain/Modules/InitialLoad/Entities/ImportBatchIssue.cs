using ERP.Domain.Common;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Entities;

/// <summary>
/// Issue (error o warning) de una <see cref="ImportBatchRow"/>. Agregado propio, no colección
/// hija en memoria (ver nota de diseño en <see cref="ImportBatch"/>) — una fila puede tener
/// varios issues y las pantallas de Preview los consultan paginados/filtrados por severidad.
/// <see cref="RowNumber"/> está denormalizado desde la fila para permitir listarlos sin join.
/// </summary>
public sealed class ImportBatchIssue : AuditableEntity, ICompanyOperationalEntity
{
    public const int CodeMaxLen = 60;
    public const int FieldNameMaxLen = 100;
    public const int MessageMaxLen = 500;

    public Guid CompanyId { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public Guid ImportBatchRowId { get; private set; }
    public int RowNumber { get; private set; }
    public string? FieldName { get; private set; }
    public ImportSeverity Severity { get; private set; }
    public string Code { get; private set; } = null!;
    public string Message { get; private set; } = null!;

    private ImportBatchIssue() { }

    public static ImportBatchIssue Create(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        Guid importBatchRowId,
        int rowNumber,
        ImportSeverity severity,
        string code,
        string message,
        Guid createdBy,
        string? fieldName = null
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código del issue es obligatorio.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("El mensaje del issue es obligatorio.", nameof(message));

        var issue = new ImportBatchIssue
        {
            TenantId = tenantId,
            CompanyId = companyId,
            ImportBatchId = importBatchId,
            ImportBatchRowId = importBatchRowId,
            RowNumber = rowNumber,
            Severity = severity,
            Code = code.Trim(),
            Message = message.Trim(),
            FieldName = string.IsNullOrWhiteSpace(fieldName) ? null : fieldName.Trim(),
        };
        issue.SetCreated(createdBy);
        return issue;
    }
}
