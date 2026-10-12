using ERP.Application.Modules.Inventory.ItemMatching.Mapping;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

public sealed record PurchaseDraftLinePackagingSnapshot(
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor
)
{
    public static PurchaseDraftLinePackagingSnapshot Base(string? baseUomCode = null)
    {
        var uom = string.IsNullOrWhiteSpace(baseUomCode) ? "UNIT" : baseUomCode.Trim();
        return new(null, uom, uom, 1m);
    }
}

public static class PurchaseDraftMapper
{
    public static PurchaseDraftDto ToDto(
        PurchaseDraft draft,
        IReadOnlyDictionary<Guid, PurchaseDraftLinePackagingSnapshot>? packagingByLine = null
    ) =>
        new(
            draft.SupplierId,
            draft.SupplierRuc,
            draft.SupplierName,
            draft.DocTypeCode,
            draft.InvoiceNumber,
            draft.IssueDate,
            draft.AccessKey,
            draft.AuthorizationNumber,
            draft.AuthorizationDate,
            draft.SriPaymentMethodCode,
            draft
                .Lines.Select(l =>
                    ToLineDto(
                        l,
                        packagingByLine is not null
                        && packagingByLine.TryGetValue(l.Id, out var snapshot)
                            ? snapshot
                            : PurchaseDraftLinePackagingSnapshot.Base()
                    )
                )
                .ToList(),
            ProcessingStatus: PurchaseReceptionMapper.ToProcessingStatusCode(
                draft.ProcessingStatus
            ),
            ProcessingNotes: draft.ProcessingNotes
        );

    /// <summary>
    /// Línea de recepción → contrato de evidencia XML del borrador. Compartido por "Crear compra"
    /// desde Recepción y por la lectura de un borrador ya guardado (PURCHASE-DRAFT-RECOVERY F1),
    /// para que el frontend reconstruya los mismos campos xml* en ambos caminos.
    /// </summary>
    public static PurchaseDraftLineDto ToLineDto(
        PurchaseDraftMergedLine l,
        PurchaseDraftLinePackagingSnapshot packaging
    ) =>
        new(
            PurchaseReceptionLineId: l.Id,
            ItemId: l.ItemId,
            ItemMatchStatus: ItemMatchingMapper.ToStatusCode(l.MatchStatus),
            Description: l.Description,
            Quantity: l.Quantity,
            UnitPrice: l.UnitPrice,
            VatCode: l.VatCode,
            WarehouseId: null,
            Notes: null,
            DiscountPct: l.DiscountPct,
            IceCode: l.IceCode,
            SupplierCode: l.SupplierCode,
            SupplierAuxCode: l.SupplierAuxCode,
            Discount: l.Discount,
            LineSubtotal: l.LineSubtotal,
            TaxCode: l.TaxCode,
            VatPercentage: l.VatPercentage,
            TaxValue: l.TaxValue,
            TotalLine: l.TotalLine,
            PackagingLevelId: packaging.PackagingLevelId,
            UomCode: packaging.UomCode,
            BaseUomCode: packaging.BaseUomCode,
            ConversionFactor: packaging.ConversionFactor,
            QuantityInBaseUom: l.Quantity * packaging.ConversionFactor,
            Taxes: l.Taxes.Select(t => new PurchaseDraftLineTaxDto(
                    t.TaxCode,
                    t.TaxRateCode,
                    t.Tarifa,
                    t.TaxableBase,
                    t.TaxAmount
                ))
                .ToList(),
            AdditionalFields: l.AdditionalFields.Select(
                    f => new PurchaseDraftLineAdditionalFieldDto(f.Name, f.Value, f.Position)
                )
                .ToList()
        );
}
