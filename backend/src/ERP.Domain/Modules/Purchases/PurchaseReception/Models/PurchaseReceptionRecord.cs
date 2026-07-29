using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>
/// Una fila ya parseada del TXT de recepción SRI, sin ningún cruce contra el ERP todavía.
/// Inmutable — el parser solo interpreta texto, no conoce base de datos.
/// </summary>
public sealed record PurchaseReceptionRecord(
    int SourceLineNumber,
    PurchaseReceptionSourceDocType SourceDocType,
    string SupplierRuc,
    string SupplierName,
    string InvoiceNumber,
    string AccessKey,
    DateOnly IssueDate,
    DateTime AuthorizationDate,
    string? ReceiverIdentification,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    string? ModifiedDocumentNumber
);
