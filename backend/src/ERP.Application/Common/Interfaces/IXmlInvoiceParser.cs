using ERP.Domain.Common;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Parsea un comprobante electrónico SRI Ecuador (XML) y extrae los datos relevantes.
/// </summary>
public interface IXmlInvoiceParser
{
    Task<InvoiceParseResult> ParseAsync(Stream xmlStream, CancellationToken cancellationToken = default);
    Task<SupplierNoteParseResult> ParseSupplierNoteAsync(Stream xmlStream, CancellationToken cancellationToken = default);
}

public sealed record InvoiceParseResult(
    string AccessKey,
    string InvoiceNumber,
    DateTime IssueDate,
    string SupplierRuc,
    string SupplierLegalName,
    decimal Subtotal,
    decimal VatTotal,
    decimal Total,
    IReadOnlyList<InvoiceLineItem> Items
);

public sealed record InvoiceLineItem(
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal
);

public sealed record SupplierNoteParseResult(
    NoteType NoteType,
    string Reason,
    string AccessKey,
    string EstabCode,
    string EmPointCode,
    string Sequential,
    string NoteNumber,
    DateTime IssueDate,
    string SupplierRuc,
    string SupplierLegalName,
    decimal Subtotal,
    decimal VatTotal,
    decimal Total,
    IReadOnlyList<SupplierNoteItem> Items
);

public sealed record SupplierNoteItem(
    string ProductCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total
);
