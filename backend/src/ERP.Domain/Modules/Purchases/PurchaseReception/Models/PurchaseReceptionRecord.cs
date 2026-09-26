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
    /// <summary>
    /// FECHA_AUTORIZACION tal como la publica el SRI en el TXT: hora de pared Ecuador/empresa, SIN
    /// offset (Kind=Unspecified). No es un instante todavía — ZH-TEMPORAL-CONTRACT-02: se convierte
    /// a UTC una sola vez en Application con <c>ICompanyClock.CompanyLocalToUtcAsync</c>.
    /// </summary>
    DateTime AuthorizationLocalDateTime,
    string? ReceiverIdentification,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    string? ModifiedDocumentNumber
);
