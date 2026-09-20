using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using MediatR;
using FluentValidation;
using ERP.Application.Modules.Pricing.UseCases.PricingRules;

namespace ERP.Application.Modules.Pricing.UseCases.ItemPricingSimulation;

/// <summary>
/// Una fila por PriceList activa y vigente. Precio íntegramente calculado en memoria — nada
/// se persiste. <see cref="NetPrice"/>/<see cref="MinAllowedPrice"/> son precios ANTES de
/// impuestos — Pricing nunca calcula IVA/ICE (frontera con la infraestructura tributaria
/// congelada, ver <see cref="Services.IPricingResolver"/>). El consumidor de esta simulación
/// aplica impuestos con su propio resolver tributario si necesita mostrar un precio final.
/// </summary>
public sealed record ItemPricingSimulationRowDto(
    Guid PriceListId,
    string PriceListCode,
    string PriceListName,
    string CurrencyCode,
    bool IsAssigned,
    PricingRuleSummaryDto RuleSummary,
    decimal NetPrice,
    decimal? MaxDiscountPercent,
    decimal MinAllowedPrice
);

/// <summary>
/// Simulación de precio contra TODAS las listas de precio activas y vigentes, en una sola
/// llamada. Reutiliza exactamente la misma precedencia de reglas que <see cref="IPricingResolver"/>
/// (vía <see cref="PricingCalculation"/>) — no reimplementa ni recalcula nada del motor de
/// Pricing, solo lo aplica en batch.
///
/// Los overrides son "qué pasaría si" (edición en curso, aún no guardada) — si se omiten,
/// se usa el valor ya persistido en el ítem. Nada de esto se persiste.
///
/// <see cref="ItemId"/> es opcional: sin él (ítem todavía no creado), no hay reglas por-ítem
/// ni asignaciones a listas posibles — la simulación corre solo con las reglas generales de
/// cada PriceList sobre <see cref="BaseSalePriceOverride"/> (obligatorio en ese caso).
/// </summary>
public sealed record GetItemPricingSimulationQuery(
    Guid? ItemId,
    decimal? BaseSalePriceOverride = null,
    decimal? MaxDiscountPercentOverride = null,
    SetPricingRuleCommand? ExceptionPreview = null
) : IRequest<Result<IReadOnlyList<ItemPricingSimulationRowDto>>>, ICompanyScopedRequest;

public sealed class GetItemPricingSimulationQueryValidator : AbstractValidator<GetItemPricingSimulationQuery>
{
    public GetItemPricingSimulationQueryValidator()
    {
        When(q => q.ExceptionPreview != null, () =>
        {
            RuleFor(q => q.ExceptionPreview!).SetValidator(new SetPricingRuleCommandValidator());
            RuleFor(q => q.ItemId).NotNull().Equal(q => q.ExceptionPreview!.ItemId);
        });
    }
}

public sealed class GetItemPricingSimulationQueryHandler
    : IRequestHandler<
        GetItemPricingSimulationQuery,
        Result<IReadOnlyList<ItemPricingSimulationRowDto>>
    >
{
    private readonly IItemRepository _items;
    private readonly IPriceListRepository _priceLists;
    private readonly IPricingRuleRepository _rules;
    private readonly IPriceListItemRepository _assignments;
    private readonly IPricingAdjustmentStrategyResolver _strategies;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICompanyClock _companyClock;

    public GetItemPricingSimulationQueryHandler(
        IItemRepository items,
        IPriceListRepository priceLists,
        IPricingRuleRepository rules,
        IPriceListItemRepository assignments,
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
        _strategies = strategies;
        _t = t;
        _c = c;
        _companyClock = companyClock;
    }

    public async Task<Result<IReadOnlyList<ItemPricingSimulationRowDto>>> Handle(
        GetItemPricingSimulationQuery q,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;

        decimal? basePrice;
        decimal? maxDiscount;
        IReadOnlyDictionary<Guid, PricingRule> itemRulesByList;
        HashSet<Guid> assignedListIds;

        if (q.ItemId.HasValue)
        {
            var item = await _items.GetByIdLightAsync(q.ItemId.Value, tenantId, ct);
            if (item is null)
                return Result<IReadOnlyList<ItemPricingSimulationRowDto>>.NotFound(
                    "Ítem no encontrado."
                );

            basePrice = q.BaseSalePriceOverride ?? item.BaseSalePrice;
            maxDiscount = q.MaxDiscountPercentOverride ?? item.SaleConfig.MaxDiscountPercent;

            itemRulesByList = (await _rules.GetByItemAsync(tenantId, q.ItemId.Value, ct))
                .Where(r => r.IsActive)
                .ToDictionary(r => r.PriceListId);

            assignedListIds = (await _assignments.GetByItemAsync(tenantId, q.ItemId.Value, ct))
                .Where(a => a.IsActive)
                .Select(a => a.PriceListId)
                .ToHashSet();
        }
        else
        {
            // Ítem aún no creado: no existe ItemId para tener reglas propias ni asignaciones
            // — solo pueden aplicar las reglas generales de cada PriceList.
            basePrice = q.BaseSalePriceOverride;
            maxDiscount = q.MaxDiscountPercentOverride;
            itemRulesByList = new Dictionary<Guid, PricingRule>();
            assignedListIds = [];
        }

        PricingRule? previewRule = null;
        if (q.ExceptionPreview is { } preview)
        {
            if (q.ItemId != preview.ItemId || !assignedListIds.Contains(preview.PriceListId))
                return Result<IReadOnlyList<ItemPricingSimulationRowDto>>.ValidationFailure(
                    "El ítem no está asignado a esta lista de precios (o la asignación está deshabilitada).");
            previewRule = PricingRule.Create(tenantId, _c.CompanyId, preview.PriceListId,
                preview.ItemId, preview.RuleType, preview.RuleValue, Guid.Empty);
        }

        if (!basePrice.HasValue)
            return Result<IReadOnlyList<ItemPricingSimulationRowDto>>.Success(
                Array.Empty<ItemPricingSimulationRowDto>()
            );

        // 2 queries totales (o 1 sin ItemId), sin importar cuántas listas existan (nunca N+1).
        var today = await _companyClock.TodayAsync(_c.CompanyId, tenantId, ct);
        var activeLists = (await _priceLists.GetAllAsync(tenantId, true, null, ct))
            .Where(pl => pl.IsValidOn(today))
            .ToList();

        var rows = new List<ItemPricingSimulationRowDto>(activeLists.Count);
        foreach (var priceList in activeLists)
        {
            var isAssigned = assignedListIds.Contains(priceList.Id);

            // PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02: con ItemId presente, ni la excepción ni la
            // regla general de la lista aplican sin una PriceListItem activa para (priceList.Id,
            // itemId) — mismo criterio que PricingResolver.ResolveAsync. Sin ItemId (ítem aún no
            // creado) el concepto de asignación no existe todavía: se preserva el comportamiento
            // documentado de "corre solo con las reglas generales de cada PriceList" (IsAssigned
            // se sigue reportando false, sin item no hay asignación real que reportar).
            var appliesRule = !q.ItemId.HasValue || isAssigned;

            decimal netPrice;
            PricingRuleSummaryDto ruleSummary;
            if (appliesRule)
            {
                itemRulesByList.TryGetValue(priceList.Id, out var itemRule);
                if (previewRule?.PriceListId == priceList.Id)
                    itemRule = previewRule;
                (netPrice, _) = PricingCalculation.Resolve(basePrice.Value, itemRule, priceList, _strategies);
                ruleSummary = PricingCalculation.Summarize(itemRule, priceList);
            }
            else
            {
                netPrice = Math.Round(basePrice.Value, 6, MidpointRounding.AwayFromZero);
                ruleSummary = new PricingRuleSummaryDto(PriceSource.BasePrice, null, null, "Precio base");
            }

            // Precio neto (sin impuestos) — el piso de descuento se calcula sobre el mismo
            // precio neto, nunca sobre un precio con IVA/ICE incluido (eso es responsabilidad
            // del consumidor tributario, no de Pricing).
            var minAllowedPrice = maxDiscount.HasValue
                ? Math.Round(
                    netPrice * (1 - maxDiscount.Value / 100m),
                    2,
                    MidpointRounding.AwayFromZero
                )
                : netPrice;

            rows.Add(
                new ItemPricingSimulationRowDto(
                    priceList.Id,
                    priceList.Code,
                    priceList.Name,
                    priceList.CurrencyCode,
                    isAssigned,
                    ruleSummary,
                    netPrice,
                    maxDiscount,
                    minAllowedPrice
                )
            );
        }

        return Result<IReadOnlyList<ItemPricingSimulationRowDto>>.Success(rows);
    }
}
