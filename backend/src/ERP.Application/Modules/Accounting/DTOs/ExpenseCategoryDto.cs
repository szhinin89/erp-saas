namespace ERP.Application.Modules.Accounting.DTOs;

public sealed record ExpenseCategoryDto(
    Guid Id,
    string Category,
    Guid ExpenseAccountId);
