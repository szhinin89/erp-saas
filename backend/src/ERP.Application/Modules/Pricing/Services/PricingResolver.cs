using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Application.Modules.Pricing.Services;

public sealed class PricingResolver : IPricingResolver
{
    // PRICE-LIST-EXPIRED-FALLBACK-PVP-01: sentinel de "sin lista aplicable" — mismo vocabulario
    // que PricingCalculation.Summarize usa para PriceSource.BasePrice ("Precio base"), nunca un
    // código/nombre de una PriceList real.
    private const string BasePriceCode = "PVP";
    private const string BasePriceName = "Precio base";

    // Fallback de moneda cuando no hay ninguna PriceList de la que tomar CurrencyCode — mismo
    // default que Company.CurrencyCode (Domain), no un valor inventado aquí.
    private const string DefaultCurrencyCode = "USD";

    private readonly IItemRepository _items;
    private readonly IPriceListRepository _priceLists;
    private readonly IPricingRuleRepository _rules;
    private readonly IPricingAdjustmentStrategyResolver _strategies;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICompanyClock _companyClock;

    public PricingResolver(
        IItemRepository items,
        IPriceListRepository priceLists,
        IPricingRuleRepository rules,
        IPricingAdjustmentStrategyResolver strategies,
        ICurrentTenant t,
        ICurrentCompany c,
        ICompanyClock companyClock
    )
    {
        _items = items;
        _priceLists = priceLists;
        _rules = rules;
        _strategies = strategies;
        _t = t;
        _c = c;
        _companyClock = companyClock;
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

        // 2. Resolve PriceList — PRICE-LIST-EXPIRED-FALLBACK-PVP-01: una lista inexistente,
        // deshabilitada, vencida o aún no vigente NUNCA bloquea la venta. Se ignora y se cae al
        // PVP/precio base del ítem, exactamente como si el ítem no tuviera ninguna lista asignada
        // — el único bloqueo real de este método es la ausencia de BaseSalePrice (arriba).
        PriceList? priceList = priceListId.HasValue
            ? await _priceLists.GetByIdAsync(tenantId, priceListId.Value, ct)
            : (await _priceLists.GetAllAsync(tenantId, true, null, ct)).FirstOrDefault(pl =>
                pl.IsDefault
            );

        var companyToday = await _companyClock.TodayAsync(_c.CompanyId, tenantId, ct);
        var listApplies = priceList is not null && priceList.IsActive && priceList.IsValidOn(companyToday);

        if (!listApplies)
        {
            return Result<PricingResult>.Success(
                new PricingResult(
                    itemId,
                    null,
                    BasePriceCode,
                    BasePriceName,
                    priceList?.CurrencyCode ?? DefaultCurrencyCode,
                    basePrice,
                    null,
                    Math.Round(basePrice, 6, MidpointRounding.AwayFromZero)
                )
            );
        }

        // 3. Resolve Rule — regla específica del ítem (si existe) > regla general de la lista > sin ajuste
        var itemRule = await _rules.GetActiveForItemInListAsync(tenantId, priceList!.Id, itemId, ct);

        // 4. Precedencia + redondeo — núcleo compartido con la simulación batch (GetItemPricingSimulation).
        var (unitPrice, ruleApplied) = PricingCalculation.Resolve(
            basePrice,
            itemRule,
            priceList,
            _strategies
        );
        var ruleDescription =
            ruleApplied is null ? null : PricingCalculation.Summarize(itemRule, priceList).Description;

        return Result<PricingResult>.Success(
            new PricingResult(
                itemId,
                priceList.Id,
                priceList.Code,
                priceList.Name,
                priceList.CurrencyCode,
                basePrice,
                ruleApplied,
                unitPrice,
                ruleDescription
            )
        );
    }
}
