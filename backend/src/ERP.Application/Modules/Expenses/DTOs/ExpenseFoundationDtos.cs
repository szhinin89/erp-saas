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
    bool IsDeductible,
    bool RequiresInvoice,
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
    bool IsDeductible,
    bool RequiresInvoice,
    bool IsActive,
    IReadOnlyList<ExpenseCategoryTreeNodeDto> Children,
    // EXPENSE-BACKEND-01: código y nombre de la cuenta contable vinculada a la subcategoría (solo
    // lectura, de la misma empresa). Permite al formulario de gastos mostrar y validar la cuenta
    // destino sin consultar el plan de cuentas (que exige accounting.view). Null si no hay cuenta o
    // no existe en la empresa. No expone ninguna otra cuenta.
    string? AccountingAccountCode = null,
    string? AccountingAccountName = null
);

public sealed record ExpenseLineDto(
    Guid Id,
    Guid ExpenseSubcategoryId,
    Guid? SnapshotAccountingAccountId,
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
