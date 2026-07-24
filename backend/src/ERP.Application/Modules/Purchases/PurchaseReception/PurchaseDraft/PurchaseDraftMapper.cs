using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

public static class PurchaseDraftMapper
{
    public static PurchaseDraftDto ToDto(PurchaseDraft draft) => new(
        draft.SupplierId, draft.SupplierRuc, draft.SupplierName,
        draft.DocTypeCode, draft.InvoiceNumber, draft.IssueDate,
        draft.AccessKey, draft.AuthorizationNumber, draft.AuthorizationDate,
        draft.SriPaymentMethodCode,
        draft.Lines.Select(l => new PurchaseDraftLineDto(
            ItemId: null, Description: l.Description, Quantity: l.Quantity, UnitPrice: l.UnitPrice,
            VatCode: l.VatCode, WarehouseId: null, Notes: null,
            DiscountPct: l.DiscountPct, IceCode: l.IceCode,
            SupplierCode: l.SupplierCode, SupplierAuxCode: l.SupplierAuxCode,
            Discount: l.Discount, LineSubtotal: l.LineSubtotal,
            TaxCode: l.TaxCode, VatPercentage: l.VatPercentage, TaxValue: l.TaxValue,
            TotalLine: l.TotalLine)).ToList());
}
