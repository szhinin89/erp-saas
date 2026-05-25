using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Fiscal.Entities;

namespace ERP.Application.Modules.Fiscal.Mapping;

internal static class InvoiceDtoMapper
{
    internal static SalesBillDto ToListDto(Invoice invoice, string? customerName = null) => new(
        invoice.PublicId,
        invoice.BusinessPartnerId,
        customerName ?? invoice.BuyerNameSnapshot,
        invoice.WarehouseId,
        invoice.BranchId,
        invoice.EstabCode,
        invoice.EmPointCode,
        invoice.Sequential,
        invoice.AccessKey,
        invoice.IssueDate,
        invoice.Subtotal,
        invoice.TaxTotal,
        invoice.Total,
        invoice.Status,
        invoice.Electronic?.AuthorizationNumber,
        invoice.Electronic?.AuthorizationDate,
        invoice.Electronic?.ErrorMessage,
        invoice.JournalEntryId,
        invoice.CreatedAt);

    internal static SalesBillDetailDto ToDetailDto(Invoice invoice, string? customerName = null)
    {
        var lines = invoice.Lines.Select(d => new SalesDetailDto(
            DeriveLineGuid(d.Id),
            d.ProductId,
            d.DescriptionSnapshot,
            d.Quantity,
            d.UnitPriceSnapshot,
            d.LineSubtotal,
            d.LineTax,
            d.LineTotal)).ToList();

        return new SalesBillDetailDto(
            invoice.PublicId,
            invoice.BusinessPartnerId,
            customerName ?? invoice.BuyerNameSnapshot,
            invoice.WarehouseId,
            invoice.BranchId,
            invoice.DocType,
            invoice.EstabCode,
            invoice.EmPointCode,
            invoice.Sequential,
            invoice.AccessKey,
            invoice.IssueDate,
            invoice.Subtotal,
            invoice.TaxTotal,
            invoice.Total,
            invoice.Status,
            invoice.Electronic?.AuthorizationNumber,
            invoice.Electronic?.AuthorizationDate,
            invoice.Electronic?.XmlSignedPath,
            invoice.Electronic?.XmlAuthorizedPath,
            invoice.Electronic?.ErrorMessage,
            invoice.JournalEntryId,
            invoice.CreatedAt,
            lines);
    }

    private static Guid DeriveLineGuid(long lineId)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, lineId);
        return new Guid(bytes);
    }
}
