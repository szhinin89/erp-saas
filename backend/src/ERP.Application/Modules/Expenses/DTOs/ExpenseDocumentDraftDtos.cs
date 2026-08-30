using ERP.Domain.Modules.Expenses.Enums;

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
    string? Notes = null
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
