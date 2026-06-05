using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Application.Modules.Expenses.DTOs;

public sealed record ExpenseInvoiceDto(
    Guid         Id,
    string?      AccessKey,
    DateTime  IssueDate,
    Guid? BusinessPartnerId,
    string?      InvoiceNumber,
    string       Description,
    string       ExpenseCategory,
    decimal      Subtotal,
    decimal   VatTotal,
    decimal      Total,
    ExpenseStatus Status,
    string?      XmlPath,
    string? Notes,
    Guid?     JournalEntryId,
    DateTime     CreatedAt);

