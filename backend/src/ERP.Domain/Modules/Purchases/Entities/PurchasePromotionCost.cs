using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>Evidence for a known zero purchase cost, after XML reconciliation.</summary>
public static class PurchasePromotionCost
{
    public static bool IsDocumentedFreeEntry(PurchaseInvoice invoice, PurchaseInvoiceDetail line,
        PurchaseReceptionDocument source, Guid productId, Guid warehouseId, decimal quantity,
        string uomCode, decimal? unitCost, decimal? totalCost) =>
        invoice.Status == PurchaseStatus.Confirmed
        && invoice.Lines.Contains(line) && line.InvoiceId == invoice.Id
        && line.TenantId == invoice.TenantId && line.IsFrozen && !line.IsXmlBreakdown
        && line.ItemId == productId && (line.WarehouseId ?? invoice.GlobalWarehouseId) == warehouseId
        && quantity > 0m && line.QuantityInBaseUom == quantity && line.BaseUomCode == uomCode
        && unitCost == 0m && totalCost == 0m && line.LandedUnitCost == 0m && line.TotalLineCost == 0m
        && line.DiscountPct == 100m && line.DiscountAmount > 0m && line.OperationalCostBase == 0m
        && source.TenantId == invoice.TenantId && source.CompanyId == invoice.CompanyId
        && source.BranchId == invoice.BranchId && source.SupplierRuc == invoice.SupplierTaxId
        && (!source.SupplierId.HasValue || source.SupplierId == invoice.SupplierId)
        && source.InvoiceNumber == invoice.InvoiceNumber && source.IssueDate == invoice.IssueDate
        && source.CurrencyCode == invoice.CurrencyCode
        && (!source.PurchaseId.HasValue || source.PurchaseId == invoice.Id)
        && (string.IsNullOrWhiteSpace(invoice.AccessKey) || source.AccessKey == invoice.AccessKey)
        && !string.IsNullOrWhiteSpace(source.XmlContent)
        && source.Lines.Any(o => o.Id == line.PurchaseReceptionLineId
            && o.TenantId == invoice.TenantId && o.Quantity > 0m && o.UnitPrice > 0m
            && o.DiscountPct == 100m && o.Discount > 0m && o.LineSubtotal == 0m
            && o.Discount == Math.Round(o.Quantity * o.UnitPrice, FiscalPrecision.TaxAmount,
                MidpointRounding.AwayFromZero));
}
