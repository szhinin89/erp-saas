using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Application.Modules.Expenses.DTOs;

public sealed record ExpenseCategoryNodeDto(
    Guid Id,
    Guid CompanyId,
    Guid? ParentId,
    string Code,
    string Name,
    string? Description,
    ExpenseCategoryNodeLevel Level,
    Guid? AccountingAccountId,
    bool IsActive
);

public sealed record ExpenseCategoryTreeNodeDto(
    Guid Id,
    Guid CompanyId,
    Guid? ParentId,
    string Code,
    string Name,
    string? Description,
    ExpenseCategoryNodeLevel Level,
    Guid? AccountingAccountId,
    bool IsActive,
    IReadOnlyList<ExpenseCategoryTreeNodeDto> Children
);

public sealed record ExpenseLineDto(
    Guid Id,
    Guid ExpenseSubcategoryId,
    Guid SnapshotAccountingAccountId,
    string? SnapshotAccountingAccountCode,
    string? SnapshotAccountingAccountName,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal DiscountAmount,
    string VatCode,
    decimal VatRate,
    decimal VatAmount,
    decimal TaxInclusiveTotal,
    short SortOrder,
    string? Notes = null
);

public sealed record ExpensePaymentScheduleDto(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    string? Notes
);

public sealed record ExpenseDocumentDto(
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
    IReadOnlyList<ExpensePaymentScheduleDto> PaymentSchedules
);
