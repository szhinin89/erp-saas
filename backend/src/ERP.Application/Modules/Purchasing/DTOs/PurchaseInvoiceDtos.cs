using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Application.Modules.Purchasing.DTOs;

public record PurchaseBillLineDto(
    Guid     Id,
    Guid?     ProductId,
    string  Description,
    string?  SupplierPrimaryCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal  DiscountPercentage,
    decimal  Subtotal,
    decimal  VatPercentage,
    decimal  VatAmount,
    decimal  Total);

public record PurchBillDto(
    Guid          Id,
    Guid    BusinessPartnerId,
    string        InvoiceNumber,
    string?       AccessKey,
    string?       XmlPath,
    DateTime      InvoiceDate,
    DateTime?     DueDate,
    PurchaseStatus  Status,
    string        PaymentTerms,
    decimal       Subtotal,
    decimal       VatTotal,
    decimal       Total,
    string? Notes,
    Guid?     JournalEntryId,
    DateTime      CreatedAt);

public record PurchBillDetailDto(
    Guid          Id,
    Guid    BusinessPartnerId,
    string        InvoiceNumber,
    string?       AccessKey,
    string?       XmlPath,
    DateTime      InvoiceDate,
    DateTime?     DueDate,
    PurchaseStatus  Status,
    string        PaymentTerms,
    decimal       Subtotal,
    decimal       VatTotal,
    decimal       Total,
    string? Notes,
    Guid?         ValidatedBy,
    DateTime?     ValidatedAt,
    Guid?         ApprovedBy,
    DateTime?     ApprovedAt,
    Guid?         RejectedBy,
    DateTime?     RejectedAt,
    string?       RejectionReason,
    Guid?     JournalEntryId,
    DateTime      CreatedAt,
    IReadOnlyList<PurchaseBillLineDto> Lines);

public record SupplierPurchaseNoteDto(
    Guid      Id,
    Guid    BusinessPartnerId,
    Guid?     PurchBillId,
    Guid?     ExpenseInvoiceId,
    NoteType  NoteType,
    string    Reason,
    string    AccessKey,
    DateTime  IssueDate,
    string    EstabCode,
    string    EmPointCode,
    string    Sequential,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string    Status,
    string?   XmlPath,
    Guid?     JournalEntryId,
    DateTime  CreatedAt);

