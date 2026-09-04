using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Retentions.DTOs;

/// <summary>
/// RETENTIONS-APPLICATION-01C — DTO de lectura de <c>RetentionDocument</c>. Sin <c>TenantId</c>,
/// mismo criterio que <c>ExpenseDocumentDetailDto</c> (Expenses no expone <c>TenantId</c> en sus
/// DTOs de salida — el tenant siempre sale del contexto autenticado, nunca es dato de negocio para
/// el cliente).
/// </summary>
public sealed record RetentionDocumentDto(
    Guid Id,
    Guid CompanyId,
    Guid BranchId,
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    Guid SubjectBusinessPartnerId,
    Guid EmissionPointId,
    string? RetentionNumber,
    DateOnly? IssueDate,
    RetentionStatus Status,
    decimal TotalRetainedVat,
    decimal TotalRetainedIncome,
    decimal TotalRetained,
    string? CancelReason,
    DateTime? CancelledAt,
    Guid? CancelledBy,
    IReadOnlyList<RetentionDocumentLineDto> Lines,
    // RETENTIONS-TAX-COMPONENT-MODEL-02B — periodo fiscal (derivado, null en Draft) y snapshot del
    // documento sustento, expuestos de lectura para que un futuro consumidor (XML/RIDE) no
    // necesite volver a resolverlos contra el documento origen.
    string? FiscalPeriod = null,
    string? SourceDocumentSriTypeCode = null,
    string? SourceDocumentNumber = null,
    DateOnly? SourceDocumentIssueDate = null,
    string? SourceDocumentAuthorizationNumber = null,
    string? SourceDocumentTaxSupportCode = null,
    decimal? SourceDocumentSubtotal = null,
    decimal? SourceDocumentTotal = null
);

public sealed record RetentionDocumentLineDto(
    Guid Id,
    RetentionTaxType TaxType,
    string RetentionCode,
    decimal BaseAmount,
    decimal RetentionRate,
    decimal RetainedAmount,
    string? Description,
    string? RetentionCodeDescription = null
);
