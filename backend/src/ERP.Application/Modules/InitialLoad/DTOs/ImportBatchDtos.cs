using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Application.Modules.InitialLoad.DTOs;

public sealed record ImportBatchDto(
    Guid Id,
    ImportType ImportType,
    ImportStatus Status,
    string? Label,
    int TotalRows,
    int ValidRows,
    int IssueRows,
    int WarningRows,
    int ImportedRows,
    DateTime? ValidatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    string? FailureReason,
    DateTime CreatedAt
)
{
    public static ImportBatchDto From(ImportBatch batch) =>
        new(
            batch.Id,
            batch.ImportType,
            batch.Status,
            batch.Label,
            batch.TotalRows,
            batch.ValidRows,
            batch.IssueRows,
            batch.WarningRows,
            batch.ImportedRows,
            batch.ValidatedAt,
            batch.ConfirmedAt,
            batch.CancelledAt,
            batch.FailureReason,
            batch.CreatedAt
        );
}

public sealed record ImportBatchIssueDto(
    Guid Id,
    int RowNumber,
    string? FieldName,
    ImportSeverity Severity,
    string Code,
    string Message
)
{
    public static ImportBatchIssueDto From(ImportBatchIssue issue) =>
        new(issue.Id, issue.RowNumber, issue.FieldName, issue.Severity, issue.Code, issue.Message);
}

public sealed record ImportBatchRowPreviewDto(
    Guid Id,
    int RowNumber,
    bool HasBlockingIssue,
    bool IsImported,
    Guid? CreatedBusinessPartnerId,
    IReadOnlyDictionary<string, string?> RawData,
    IReadOnlyList<ImportBatchIssueDto> Issues
);

public sealed record ImportBatchConfirmResultDto(
    Guid ImportBatchId,
    ImportStatus Status,
    int ImportedRows,
    int FailedRows
);

public sealed record ImportTemplateFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>Fila cruda leída del Excel por un <c>IImportProcessor</c>, keyed por encabezado de columna.</summary>
public sealed record ImportReadResult(
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows
);

public sealed record RowValidationResult(
    string ParsedDataJson,
    bool HasBlockingIssue,
    IReadOnlyList<RowIssue> Issues
);

public sealed record RowIssue(ImportSeverity Severity, string Code, string Message, string? FieldName = null);

public sealed record RowConfirmResult(bool IsSuccess, Guid? BusinessPartnerId, string? Error)
{
    public static RowConfirmResult Success(Guid businessPartnerId) => new(true, businessPartnerId, null);

    public static RowConfirmResult Failed(string error) => new(false, null, error);
}

/// <summary>Fila de Clientes ya tipada — resultado del mapeo de <c>CustomerImportProcessor</c>.</summary>
public sealed record ParsedCustomerRow(
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? CountryCode,
    string? Email,
    string? Phone,
    string? CustomerCategory,
    string? CustomerSegment,
    string? SalesZone,
    decimal? CreditLimit,
    int? PaymentDays
);
