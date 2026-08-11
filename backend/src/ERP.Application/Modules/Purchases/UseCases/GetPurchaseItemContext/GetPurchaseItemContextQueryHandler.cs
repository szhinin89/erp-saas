using ERP.Application.Common;
using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases.GetPurchaseItemContext;

public sealed class GetPurchaseItemContextQueryHandler
    : IRequestHandler<GetPurchaseItemContextQuery, Result<PurchaseItemContextDto>>
{
    private readonly IItemRepository _itemRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IPricingResolver _pricingResolver;
    private readonly ISriTaxResolver _taxResolver;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly IDecimalConfigRepository _decimalConfigRepo;

    public GetPurchaseItemContextQueryHandler(
        IItemRepository itemRepo,
        IStockRepository stockRepo,
        IPricingResolver pricingResolver,
        ISriTaxResolver taxResolver,
        ICurrentTenant tenant,
        ICurrentCompany company,
        IDecimalConfigRepository decimalConfigRepo
    )
    {
        _itemRepo = itemRepo;
        _stockRepo = stockRepo;
        _pricingResolver = pricingResolver;
        _taxResolver = taxResolver;
        _tenant = tenant;
        _company = company;
        _decimalConfigRepo = decimalConfigRepo;
    }

    public async Task<Result<PurchaseItemContextDto>> Handle(
        GetPurchaseItemContextQuery request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var decimalConfig = await _decimalConfigRepo.GetAsync(tid, _company.CompanyId, ct);

        // 1. ITEM — incluye empaques para que Compras pueda seleccionar presentación sin
        // duplicar un sistema UOM paralelo.
        var item = await _itemRepo.GetByIdAsync(request.ItemId, tid, ct);
        if (item is null)
            return Result<PurchaseItemContextDto>.NotFound("Ítem no encontrado.");

        // 1b. CÓDIGO DE PROVEEDOR — ItemSupplierCode es la única fuente (multi-proveedor,
        // principal si hay varios). Sin proveedor indicado, o sin código registrado para
        // ese proveedor, no hay código que mostrar (null) — el ítem no tiene código de
        // compra global (Item.Code.PurchaseCode legacy fue eliminado).
        string? supplierCode = null;
        if (request.SupplierId.HasValue)
            supplierCode = await _itemRepo.GetSupplierCodeAsync(
                request.ItemId,
                request.SupplierId.Value,
                tid,
                ct
            );

        // 2. STOCK — SSOT desde CurrentStock
        var stock = await _stockRepo.GetStockAsync(tid, request.WarehouseId, request.ItemId, ct);
        var currentQty = stock?.Quantity ?? 0m;
        var availableQty = stock?.AvailableQuantity ?? 0m;
        var reservedQty = stock?.ReservedQuantity ?? 0m;
        var averageCost = stock?.AverageCost ?? 0m;

        // 3. ÚLTIMO COSTO — desde StockMovement (PurchaseEntry más reciente)
        var lastCost =
            await _stockRepo.GetLastPurchaseCostAsync(tid, request.ItemId, request.WarehouseId, ct)
            ?? 0m;

        // 4. PVP — SSOT vía PricingResolver (Motor de Pricing). Sin precio configurado
        // o sin lista predeterminada, se muestra 0 — es solo contexto informativo para
        // la línea de compra, no bloquea el flujo.
        decimal pvp = 0m;
        var pricingResult = await _pricingResolver.ResolveAsync(request.ItemId, ct: ct);
        if (pricingResult.IsSuccess)
            pvp = pricingResult.Value!.UnitPrice;

        // 5. DESCUENTO — MaxDiscountPercent del item
        var maxDiscount = item.SaleConfig?.MaxDiscountPercent ?? 0m;

        // 6. IMPUESTOS — desde catálogo SRI
        var hasVat = !string.IsNullOrWhiteSpace(item.TaxConfig.PurchaseVatCode);
        var hasIce = !string.IsNullOrWhiteSpace(item.TaxConfig.ExciseTaxCode);
        decimal vatPct = 0m;
        decimal icePct = 0m;

        var purchaseVatCode = item.TaxConfig.PurchaseVatCode;
        if (hasVat && !string.IsNullOrWhiteSpace(purchaseVatCode))
            vatPct = await _taxResolver.GetVatRateAsync(purchaseVatCode, ct) ?? 0m;

        var exciseTaxCode = item.TaxConfig.ExciseTaxCode;
        if (hasIce && !string.IsNullOrWhiteSpace(exciseTaxCode))
            icePct = await _taxResolver.GetIceRateAsync(exciseTaxCode, ct) ?? 0m;

        // 7. MARGEN — costo promedio vs PVP
        var costMargin = pvp - averageCost;
        var costMarginPct = pvp > 0m ? (costMargin / pvp) * 100m : 0m;

        return Result<PurchaseItemContextDto>.Success(
            new PurchaseItemContextDto
            {
                ItemId = item.Id,
                Sku = item.Code.SKU,
                ShortName = item.Code.ShortName,
                Description = item.Code.Description,
                BaseUomCode = item.DefaultUomCode,
                PackagingLevels = item
                    .PackagingLevels.Where(p => p.IsActive)
                    .OrderBy(p => p.Level)
                    .Select(p => new PurchaseItemPackagingLevelDto(
                        p.Id,
                        p.Name,
                        p.BaseQuantity,
                        p.UomCode,
                        p.IsBaseUnit,
                        p.IsPurchaseDefault
                    ))
                    .ToList(),
                SupplierCode = supplierCode,

                CurrentStock = currentQty,
                AvailableStock = availableQty,
                ReservedStock = reservedQty,

                AverageCost = Math.Round(averageCost, decimalConfig.PurchaseUnitPrice),
                LastPurchaseCost = Math.Round(lastCost, decimalConfig.PurchaseUnitPrice),

                Pvp = Math.Round(pvp, decimalConfig.SalesUnitPrice),
                PreviousPrice = Math.Round(pvp, decimalConfig.SalesUnitPrice),
                MaxDiscountPercent = maxDiscount,

                PurchaseVatCode = item.TaxConfig.PurchaseVatCode,
                VatPercent = vatPct,
                ExciseTaxCode = item.TaxConfig.ExciseTaxCode,
                IcePercent = icePct,
                HasVat = hasVat,
                HasIce = hasIce,

                CostMargin = Math.Round(costMargin, decimalConfig.TotalAmount),
                CostMarginPercent = Math.Round(costMarginPct, decimalConfig.Percentage),
            }
        );
    }
}
