using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Sales;

internal static class SalesReturnMapper
{
    public static SalesReturnDto ToDto(SalesReturn r) =>
        new(
            r.Id,
            r.SalesInvoiceId,
            r.CustomerId,
            r.ReturnNumber,
            r.Reason,
            r.Status.ToString(),
            r.Subtotal,
            r.TotalVat,
            r.TotalIce,
            r.TotalDiscount,
            r.GrandTotal,
            r.Lines.Select(MapDetail).ToList(),
            r.RefundAllocations.Select(MapAllocation).ToList(),
            r.CreatedAt,
            r.UpdatedAt
        );

    public static SalesReturnListDto ToListDto(SalesReturn r) =>
        new(
            r.Id,
            r.ReturnNumber,
            r.SalesInvoiceId,
            r.CustomerId,
            r.Status.ToString(),
            r.Lines.Count,
            r.GrandTotal,
            r.CreatedAt
        );

    private static SalesReturnDetailDto MapDetail(SalesReturnDetail d) =>
        new(
            d.Id,
            d.OriginalInvoiceDetailId,
            d.ItemId,
            d.Description,
            d.SnapshotSku,
            d.SnapshotItemName,
            d.WarehouseId,
            d.UomCode,
            d.Quantity,
            d.UnitPrice,
            d.DiscountPct,
            d.DiscountAmount,
            d.VatCode,
            d.VatRate,
            d.VatAmount,
            d.IceCode,
            d.IceRate,
            d.IceAmount,
            d.LineSubtotal,
            d.TaxableBase,
            d.TaxInclusiveTotal,
            d.IsFrozen,
            d.PackagingLevelId,
            d.BaseUomCode,
            d.ConversionFactor,
            d.QuantityInBaseUom
        );

    private static SalesReturnRefundAllocationDto MapAllocation(SalesReturnRefundAllocation a) =>
        new(a.Id, a.Method.ToString(), a.Amount);
}
