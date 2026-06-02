using ERP.Domain.Common;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Parsea un comprobante electrÃ³nico SRI Ecuador (XML) y extrae los datos relevantes.
/// </summary>
public interface IXmlFacturaParser
{
    Task<FacturaParseResult> ParseAsync(Stream xmlStream, CancellationToken ct = default);
    Task<SupplierNoteParseResult> ParseSupplierNoteAsync(Stream xmlStream, CancellationToken ct = default);
}

public sealed record FacturaParseResult(
    string                    AccessKey,
    string                    InvoiceNumber,
    DateTime                  IssueDate,
    string                    SupplierRuc,
    string                    SupplierLegalName,
    decimal                   Subtotal,
    decimal                   VatTotal,
    decimal                   Total,
    IReadOnlyList<ItemFactura> Items
);

public sealed record ItemFactura(
    string  ProductCode,
    string  Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal
);

public sealed record SupplierNoteParseResult(
    NoteType                  NoteType,
    string                    Reason,
    string                    AccessKey,
    string                    EstabCode,
    string                    EmPointCode,
    string                    Sequential,
    string                    NoteNumber,
    DateTime                  IssueDate,
    string                    SupplierRuc,
    string                    SupplierLegalName,
    decimal                   Subtotal,
    decimal                   VatTotal,
    decimal                   Total,
    IReadOnlyList<SupplierNoteItem> Items
);

public sealed record SupplierNoteItem(
    string  ProductCode,
    string  Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total
);
