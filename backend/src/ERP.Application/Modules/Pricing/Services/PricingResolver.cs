using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Application.Modules.Pricing.Services;

public sealed class PricingResolver : IPricingResolver
{
    private readonly IItemRepository _items;
    private readonly IPriceListRepository _priceLists;
    private readonly IPricingRuleRepository _rules;
    private readonly IPricingAdjustmentStrategyResolver _strategies;
    private readonly ICurrentTenant _t;

    public PricingResolver(
        IItemRepository items,
        IPriceListRepository priceLists,
        IPricingRuleRepository rules,
        IPricingAdjustmentStrategyResolver strategies,
        ICurrentTenant t
    )
    {
        _items = items;
        _priceLists = priceLists;
        _rules = rules;
        _strategies = strategies;
        _t = t;
    }

    public async Task<Result<PricingResult>> ResolveAsync(
        Guid itemId,
        Guid? priceListId = null,
        CancellationToken ct = default
    )
    {
        var tenantId = _t.TenantId;

        // 1. Load Item → Load Base Price
        var item = await _items.GetByIdLightAsync(itemId, tenantId, ct);
        if (item is null)
            return Result<PricingResult>.NotFound("Ítem no encontrado.");
        if (!item.BaseSalePrice.HasValue)
            return Result<PricingResult>.ValidationFailure(
                "El ítem no tiene precio base configurado. Verifique el maestro de productos."
            );
        var basePrice = item.BaseSalePrice.Value;

        // 2. Resolve PriceList
        PriceList? priceList = priceListId.HasValue
            ? await _priceLists.GetByIdAsync(tenantId, priceListId.Value, ct)
            : (await _priceLists.GetAllAsync(tenantId, true, null, ct)).FirstOrDefault(pl =>
                pl.IsDefault
            );

        if (priceList is null)
            return Result<PricingResult>.ValidationFailure(
                priceListId.HasValue
                    ? "La lista de precios indicada no existe."
                    : "No existe una lista de precios predeterminada activa. Configure una en el catálogo de Pricing."
            );
        if (!priceList.IsActive)
            return Result<PricingResult>.ValidationFailure(
                $"La lista de precios '{priceList.Code}' está deshabilitada."
            );
        if (!priceList.IsValidOn(DateOnly.FromDateTime(DateTime.UtcNow)))
            return Result<PricingResult>.ValidationFailure(
                $"La lista de precios '{priceList.Code}' no está vigente en la fecha actual."
            );

        // 3. Resolve Rule — regla específica del ítem (si existe) > regla general de la lista > sin ajuste
        var itemRule = await _rules.GetActiveForItemInListAsync(tenantId, priceList.Id, itemId, ct);

        // 4. Precedencia + redondeo — núcleo compartido con la simulación batch (GetItemPricingSimulation).
        var (unitPrice, ruleApplied) = PricingCalculation.Resolve(
            basePrice,
            itemRule,
            priceList,
            _strategies
        );

        return Result<PricingResult>.Success(
            new PricingResult(
                itemId,
                priceList.Id,
                priceList.Code,
                priceList.Name,
                priceList.CurrencyCode,
                basePrice,
                ruleApplied,
                unitPrice
            )
        );
    }
}
