using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Expenses.DTOs;

public sealed record ExpenseDraftLineRequest(
    Guid ExpenseSubcategoryId,
    string? Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountValue = 0m,
    string VatCode = "0",
    string? Notes = null
);

/// <summary>
/// RETENTIONS-API-EXPENSES-01E — línea de retención tal como llega desde el body HTTP. Espejo de
/// <see cref="ERP.Application.Modules.Retentions.UseCases.IssueRetentionLineInput"/> (mismo shape),
/// mantenido como tipo de contrato de API independiente del command interno — mismo criterio ya
/// usado por <see cref="CreateExpenseDraftRequest"/> frente a <c>CreateExpenseDraftCommand</c>.
/// </summary>
public sealed record RetentionIntentLineRequest(
    RetentionTaxType TaxType,
    string RetentionCode,
    decimal BaseAmount,
    decimal RetentionRate,
    decimal RetainedAmount,
    string? Description = null,
    // RETENTIONS-TAX-COMPONENT-MODEL-02B: opcional a nivel de contrato de API (mismo criterio que
    // IssueRetentionLineInput) — sin selector de catálogo en frontend todavía, RetentionIssuer usa
    // RetentionCode como respaldo cuando no llega.
    string? RetentionCodeDescription = null
);

/// <summary>
/// RETENTIONS-API-EXPENSES-01E — intención opcional del usuario de generar una retención en la
/// misma operación de confirmar un gasto. <c>null</c> (o <see cref="AppliesRetention"/> = false)
/// preserva exactamente el comportamiento actual (sin retención). Nunca incluye
/// <c>TenantId</c>/<c>CompanyId</c>/<c>BranchId</c> — esos siguen viniendo exclusivamente del
/// contexto autenticado en el handler de Application.
///
/// RETENTIONS-DOCUMENT-SEQUENCE-02E: ya NO acepta un número de retención manual — el servidor lo
/// genera siempre internamente a partir de <see cref="EmissionPointId"/>. Si un cliente todavía
/// envía un campo <c>retentionNumber</c> en el JSON, el binder de ASP.NET Core lo ignora en
/// silencio (no forma parte de este contrato) — nunca puede sustituir el número generado.
/// </summary>
public sealed record RetentionIntentRequest(
    bool AppliesRetention,
    Guid? EmissionPointId,
    DateOnly? IssueDate,
    IReadOnlyList<RetentionIntentLineRequest>? Lines
);

/// <summary>
/// RETENTIONS-API-EXPENSES-01E — body opcional de <c>POST {id}/confirm</c>. Sin body (o con
/// <c>Retention</c> ausente/null), el endpoint se comporta exactamente igual que antes de esta
/// fase.
/// </summary>
public sealed record ConfirmExpenseDocumentRequest(RetentionIntentRequest? Retention = null);

public sealed record CreateExpenseDraftRequest(
    Guid SupplierId,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    string DocumentType,
    string DocumentNumber,
    Guid? PaymentTermId,
    DateOnly? DueDate,
    IReadOnlyList<ExpenseDraftLineRequest> Lines,
    string? AuthorizationNumber = null,
    DateTime? AuthorizationDate = null,
    string? Notes = null,
    RetentionIntentRequest? Retention = null
);

public sealed record CancelExpenseDocumentRequest(string Reason);

public sealed record UpdateExpenseDraftRequest(
    Guid SupplierId,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    string DocumentType,
    string DocumentNumber,
    Guid? PaymentTermId,
    DateOnly? DueDate,
    IReadOnlyList<ExpenseDraftLineRequest> Lines,
    string? AuthorizationNumber = null,
    DateTime? AuthorizationDate = null,
    string? Notes = null
);

public sealed record ExpenseDocumentListItemDto(
    Guid Id,
    Guid CompanyId,
    Guid BranchId,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    string DocumentType,
    string DocumentNumber,
    DateOnly? DueDate,
    ExpenseStatus Status,
    int LineCount,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    DateTime CreatedAt
);

public sealed record ExpenseDocumentListResponse(
    IReadOnlyList<ExpenseDocumentListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

public sealed record ExpenseDocumentDetailDto(
    Guid Id,
    Guid CompanyId,
    Guid BranchId,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    string DocumentType,
    string DocumentNumber,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    Guid PaymentTermId,
    string PaymentTermName,
    DateOnly? DueDate,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    string? Notes,
    ExpenseStatus Status,
    IReadOnlyList<ExpenseLineDto> Lines,
    string? CancelReason,
    DateTime? CancelledAt,
    Guid? CancelledBy
);
