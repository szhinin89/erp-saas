using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Application.Modules.Purchases;

internal static class PurchaseMapper
{
    public static PurchaseInvoiceDto ToDto(PurchaseInvoice i) =>
        new(
            i.Id,
            i.SupplierId,
            i.SupplierName,
            i.SupplierTaxId,
            i.DocTypeCode,
            i.InvoiceNumber,
            i.IssueDate,
            i.AccessKey,
            i.AuthorizationNumber,
            i.AuthorizationDate,
            i.TaxSupportCode,
            i.SriPaymentMethodCode,
            i.SriPaymentMethodName,
            i.CurrencyCode,
            i.ExchangeRate,
            i.PurchaseOrderId,
            i.PurchaseOrderNumber,
            i.GlobalWarehouseId,
            i.PaymentTermId,
            i.PaymentTermName,
            i.PaymentTermInstallments,
            i.PaymentTermDaysBetween,
            i.CreditTermDays,
            i.DueDate,
            i.Notes,
            i.Status.ToString(),
            i.Subtotal,
            i.TotalDiscount,
            i.TotalIce,
            i.TotalVat,
            i.TotalTax,
            i.TotalFreight,
            i.TotalOtherCosts,
            i.GrandTotal,
            i.TotalIrbpnr,
            i.Lines.OrderBy(l => l.SortOrder).Select(MapLine).ToList(),
            i.PaymentSchedules.OrderBy(s => s.InstallmentNumber).Select(MapSchedule).ToList(),
            i.CancelReason,
            i.CancelledAt,
            i.CancelledBy,
            i.CreatedAt,
            i.UpdatedAt
        );

    public static PurchaseInvoiceDetailDto MapLine(PurchaseInvoiceDetail l) =>
        new(
            l.Id,
            l.ItemId,
            l.Description,
            l.SnapshotSku,
            l.SnapshotItemName,
            l.SnapshotSupplierCode,
            l.UomCode,
            l.ConversionFactor,
            l.QuantityInBaseUom,
            l.Quantity,
            l.UnitPrice,
            l.DiscountPct,
            l.DiscountAmount,
            l.FreightAllocated,
            l.OtherCostsAllocated,
            l.TotalLineCost,
            l.LandedUnitCost,
            l.TaxableBase,
            l.VatCode,
            l.VatRate,
            l.VatAmount,
            l.SnapshotVatName,
            l.IceCode,
            l.IceRate,
            l.IceAmount,
            l.SnapshotIceName,
            l.IrbpnrCode,
            l.IrbpnrRate,
            l.IrbpnrAmount,
            l.SnapshotIrbpnrName,
            l.Taxes.Select(MapTax).ToList(),
            l.TaxInclusiveTotal,
            l.SnapshotItemPvp,
            l.WarehouseId,
            l.SnapshotWarehouseCode,
            l.PurchaseOrderDetailId,
            l.OrderedQuantity,
            l.PurchaseReceptionLineId,
            l.Notes,
            l.SortOrder
        );

    public static PurchasePaymentScheduleDto MapSchedule(PurchasePaymentSchedule s) =>
        new(s.Id, s.InstallmentNumber, s.DueDate, s.Amount, s.Notes);

    public static PurchaseInvoiceDetailTaxDto MapTax(PurchaseInvoiceDetailTax t) =>
        new(
            t.TaxCode,
            t.TaxRateCode,
            t.TaxName,
            t.Rate,
            t.CalculationType.ToString(),
            t.TaxableBase,
            t.TaxAmount,
            t.Source.ToString()
        );
}
