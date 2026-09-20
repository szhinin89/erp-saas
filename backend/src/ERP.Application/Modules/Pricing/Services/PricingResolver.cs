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
    private readonly IPriceListItemRepository _assignments;
    private readonly IPriceListSelectionResolver _selection;
    private readonly IPricingAdjustmentStrategyResolver _strategies;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICompanyClock _companyClock;

    public PricingResolver(
        IItemRepository items,
        IPriceListRepository priceLists,
        IPricingRuleRepository rules,
        IPriceListItemRepository assignments,
        IPriceListSelectionResolver selection,
        IPricingAdjustmentStrategyResolver strategies,
        ICurrentTenant t,
        ICurrentCompany c,
        ICompanyClock companyClock
    )
    {
        _items = items;
        _priceLists = priceLists;
        _rules = rules;
        _assignments = assignments;
        _selection = selection;
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
            return Result<PricingResult>.Success(
                BasePriceResult(itemId, basePrice, priceList?.CurrencyCode)
            );

        var resolved = await TryResolveAgainstListAsync(tenantId, itemId, priceList!, basePrice, null, ct);
        return Result<PricingResult>.Success(
            resolved ?? BasePriceResult(itemId, basePrice, priceList!.CurrencyCode)
        );
    }

    /// <summary>
    /// PRICING-CONTEXTUAL-RESOLUTION-05C: recorre los candidatos de IPriceListSelectionResolver
    /// en orden (Customer → CompanyDefault → ...) y usa el primero cuyo ítem tenga un
    /// PriceListItem activo — exactamente el mismo criterio de asignación que ya aplica
    /// ResolveAsync(Guid, Guid?, ct) para la lista default, ahora repetido por candidato en vez
    /// de una sola lista fija. Sin CustomerId, el único candidato posible es CompanyDefault — el
    /// comportamiento es idéntico al de hoy.
    /// </summary>
    public async Task<Result<PricingResult>> ResolveAsync(
        PricingContext context,
        CancellationToken ct = default
    )
    {
        var tenantId = _t.TenantId;

        var item = await _items.GetByIdLightAsync(context.ItemId, tenantId, ct);
        if (item is null)
            return Result<PricingResult>.NotFound("Ítem no encontrado.");
        if (!item.BaseSalePrice.HasValue)
            return Result<PricingResult>.ValidationFailure(
                "El ítem no tiene precio base configurado. Verifique el maestro de productos."
            );
        var basePrice = item.BaseSalePrice.Value;

        // IPriceListSelectionResolver ya filtra vigencia/activación de cada PriceList candidata
        // (PRICE-LIST-EXPIRED-FALLBACK-PVP-01) y ya deduplica cuando la lista del cliente
        // coincide con la default (PRICING-PRICE-LIST-SELECTION-CANDIDATES-05A1) — "misma
        // evaluación" cuando ambas son la misma lista, nunca se evalúa dos veces. CustomerId nulo
        // se propaga tal cual (PRICING-CONTEXT-NULL-CUSTOMER-05C1) — nunca se sustituye por
        // Guid.Empty como sentinel.
        var candidates = await _selection.ResolveAsync(context.CustomerId, ct);

        foreach (var candidate in candidates)
        {
            var priceList = await _priceLists.GetByIdAsync(tenantId, candidate.PriceListId, ct);
            if (priceList is null)
                continue;

            var resolved = await TryResolveAgainstListAsync(
                tenantId,
                context.ItemId,
                priceList,
                basePrice,
                candidate.Source,
                ct
            );
            if (resolved is not null)
                return Result<PricingResult>.Success(resolved);
        }

        // Ningún candidato tenía el ítem asignado y activo — mismo fallback final que la lista
        // explícita/default inaplicable.
        return Result<PricingResult>.Success(BasePriceResult(context.ItemId, basePrice, null));
    }

    /// <summary>
    /// PRICING-CONTEXTUAL-BATCH-RESOLUTION-05D: misma decisión exacta que
    /// ResolveAsync(PricingContext, ct) (vía BuildResultForList, núcleo compartido), aplicada a
    /// N ítems con consultas en batch — NUNCA una por ítem.
    ///
    /// Queries totales, independientes de cuántos ítems traiga el batch:
    ///   1. IPriceListSelectionResolver.ResolveAsync — 1 llamada (que internamente hace ~2-3
    ///      queries propias, ya documentadas en PriceListSelectionResolver).
    ///   2. IItemRepository.GetByIdsLightAsync — 1 query para TODOS los ítems del batch.
    ///   3. Por cada candidato (típicamente 1-2: Customer y/o CompanyDefault): 1 query de
    ///      PriceList + 1 de PriceListItem activos de esa lista + 1 de PricingRule activas de esa
    ///      lista = 3 queries por candidato.
    /// Total aproximado: ~2 + 3×candidatos (usualmente 5-8 queries), sin importar si el batch
    /// trae 1 ítem o 500 — nunca N+1.
    /// </summary>
    public async Task<Result<IReadOnlyDictionary<Guid, PricingResult>>> ResolveManyAsync(
        PricingBatchContext context,
        CancellationToken ct = default
    )
    {
        var tenantId = _t.TenantId;
        var itemIds = context.ItemIds.Distinct().ToList();
        var results = new Dictionary<Guid, PricingResult>();
        if (itemIds.Count == 0)
            return Result<IReadOnlyDictionary<Guid, PricingResult>>.Success(results);

        // 1 query: mismos candidatos ordenados que usa el resolver single — una sola fuente de
        // verdad de "qué listas corresponden a este cliente", nunca reimplementada aquí.
        var candidates = await _selection.ResolveAsync(context.CustomerId, ct);

        // 1 query: ítems + BaseSalePrice en batch. Ítems inexistentes o sin BaseSalePrice
        // simplemente no aparecen en el resultado — un ítem inválido no invalida el batch
        // completo (a diferencia del ResolveAsync single, que sí falla para ese único ítem).
        var items = await _items.GetByIdsLightAsync(itemIds, tenantId, ct);
        var basePriceByItemId = items
            .Where(i => i.BaseSalePrice.HasValue)
            .ToDictionary(i => i.Id, i => i.BaseSalePrice!.Value);

        // 3 queries por candidato (nunca por ítem): la PriceList en sí, sus PriceListItem
        // activos (para saber qué ítems están asignados) y sus PricingRule activas (para
        // resolver excepción por ítem sin una query adicional cada vez).
        var priceListsById = new Dictionary<Guid, PriceList>();
        var assignedItemIdsByList = new Dictionary<Guid, HashSet<Guid>>();
        var rulesByListAndItem = new Dictionary<Guid, IReadOnlyDictionary<Guid, PricingRule>>();

        foreach (var candidate in candidates)
        {
            var priceList = await _priceLists.GetByIdAsync(tenantId, candidate.PriceListId, ct);
            if (priceList is null)
                continue;
            priceListsById[candidate.PriceListId] = priceList;

            var assignments = await _assignments.GetByPriceListAsync(tenantId, candidate.PriceListId, ct);
            assignedItemIdsByList[candidate.PriceListId] = assignments.Select(a => a.ItemId).ToHashSet();

            var rules = await _rules.GetByPriceListAsync(tenantId, candidate.PriceListId, ct);
            rulesByListAndItem[candidate.PriceListId] = rules.ToDictionary(r => r.ItemId);
        }

        // Decisión por ítem: 100% en memoria, ninguna consulta adicional — recorre los mismos
        // candidatos en el mismo orden que ResolveAsync(PricingContext, ct).
        foreach (var itemId in itemIds)
        {
            if (!basePriceByItemId.TryGetValue(itemId, out var basePrice))
                continue;

            PricingResult? resolved = null;
            foreach (var candidate in candidates)
            {
                if (
                    !priceListsById.TryGetValue(candidate.PriceListId, out var priceList)
                    || !assignedItemIdsByList[candidate.PriceListId].Contains(itemId)
                )
                    continue;

                rulesByListAndItem[candidate.PriceListId].TryGetValue(itemId, out var itemRule);
                resolved = BuildResultForList(itemId, priceList, basePrice, itemRule, candidate.Source, _strategies);
                break;
            }

            results[itemId] = resolved ?? BasePriceResult(itemId, basePrice, null);
        }

        return Result<IReadOnlyDictionary<Guid, PricingResult>>.Success(results);
    }

    /// <summary>
    /// PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02: la regla de una PriceList (general o excepción)
    /// solo aplica a ítems asignados y activos en ESA lista. Null = el ítem no está asignado (o
    /// la asignación está deshabilitada) — el llamador decide el siguiente paso (lista default,
    /// siguiente candidato, o PVP).
    /// </summary>
    private async Task<PricingResult?> TryResolveAgainstListAsync(
        Guid tenantId,
        Guid itemId,
        PriceList priceList,
        decimal basePrice,
        PriceListSelectionSource? selectionSource,
        CancellationToken ct
    )
    {
        var assignment = await _assignments.FindByKeyAsync(tenantId, priceList.Id, itemId, ct);
        if (assignment is not { IsActive: true })
            return null;

        var itemRule = await _rules.GetActiveForItemInListAsync(tenantId, priceList.Id, itemId, ct);
        return BuildResultForList(itemId, priceList, basePrice, itemRule, selectionSource, _strategies);
    }

    /// <summary>
    /// Núcleo puro (sin I/O) compartido por ResolveAsync y ResolveManyAsync — PRICING-CONTEXTUAL-
    /// BATCH-RESOLUTION-05D: un solo lugar decide "excepción &gt; regla general &gt; PVP" para
    /// que single y batch nunca puedan divergir. Asume que el llamador YA confirmó que el ítem
    /// está asignado (activo) a <paramref name="priceList"/> — esta función no vuelve a
    /// verificarlo.
    /// </summary>
    private static PricingResult BuildResultForList(
        Guid itemId,
        PriceList priceList,
        decimal basePrice,
        PricingRule? itemRule,
        PriceListSelectionSource? selectionSource,
        IPricingAdjustmentStrategyResolver strategies
    )
    {
        // Resolve Rule — regla específica del ítem (si existe) > regla general de la lista > sin
        // ajuste. Precedencia + redondeo: núcleo compartido con la simulación batch
        // (GetItemPricingSimulation) — nunca reimplementado aquí.
        var (unitPrice, ruleApplied) = PricingCalculation.Resolve(basePrice, itemRule, priceList, strategies);
        var ruleDescription =
            ruleApplied is null ? null : PricingCalculation.Summarize(itemRule, priceList).Description;

        return new PricingResult(
            itemId,
            priceList.Id,
            priceList.Code,
            priceList.Name,
            priceList.CurrencyCode,
            basePrice,
            ruleApplied,
            unitPrice,
            ruleDescription,
            selectionSource
        );
    }

    private static PricingResult BasePriceResult(Guid itemId, decimal basePrice, string? currencyCode) =>
        new(
            itemId,
            null,
            BasePriceCode,
            BasePriceName,
            currencyCode ?? DefaultCurrencyCode,
            basePrice,
            null,
            Math.Round(basePrice, 6, MidpointRounding.AwayFromZero)
        );
}
