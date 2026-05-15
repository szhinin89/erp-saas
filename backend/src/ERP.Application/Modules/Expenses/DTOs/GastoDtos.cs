using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Application.Modules.Expenses.DTOs;

public sealed record ExpenseInvoiceDto(
    Guid         Id,
    string?      ClaveAcceso,
    DateTime  IssueDate,
    Guid? SupplierId,
    string?      NumeroFactura,
    string       Concepto,
    string       CategoriaGasto,
    decimal      Subtotal,
    decimal   VatTotal,
    decimal      Total,
    ExpenseStatus Status,
    string?      XmlPath,
    string? Notes,
    Guid?     JournalEntryId,
    DateTime     CreatedAt);

