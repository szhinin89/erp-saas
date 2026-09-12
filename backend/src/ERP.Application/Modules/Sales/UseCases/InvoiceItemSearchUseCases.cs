using ERP.Application.Common;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

public sealed record SearchItemsForInvoiceQuery(string Query, Guid? WarehouseId, int PageSize = 10)
    : IRequest<Result<IReadOnlyList<InvoiceItemSearchResultDto>>>,
        IBranchScopedRequest;

public sealed class SearchItemsForInvoiceHandler
    : IRequestHandler<SearchItemsForInvoiceQuery, Result<IReadOnlyList<InvoiceItemSearchResultDto>>>
{
    private readonly IInvoiceItemSearchRepository _repo;
    private readonly ISriCatalogResolver _sri;
    private readonly IPriceListRepository _priceLists;
    private readonly IPricingRuleRepository _rules;
    private readonly IPricingAdjustmentStrategyResolver _strategies;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public SearchItemsForInvoiceHandler(
        IInvoiceItemSearchRepository repo,
        ISriCatalogResolver sri,
        IPriceListRepository priceLists,
        IPricingRuleRepository rules,
        IPricingAdjustmentStrategyResolver strategies,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _sri = sri;
        _priceLists = priceLists;
        _rules = rules;
        _strategies = strategies;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<IReadOnlyList<InvoiceItemSearchResultDto>>> Handle(
        SearchItemsForInvoiceQuery request,
        CancellationToken cancellationToken
    )
    {
        var q = request.Query.Trim();
        if (q.Length < 2)
            return Result<IReadOnlyList<InvoiceItemSearchResultDto>>.Success(
                Array.Empty<InvoiceItemSearchResultDto>()
            );

        var matches = await _repo.SearchAsync(
            _tenant.TenantId,
            _company.CompanyId,
            q,
            request.WarehouseId,
            Math.Clamp(request.PageSize, 1, 20),
            cancellationToken
        );

        if (matches.Count == 0)
            return Result<IReadOnlyList<InvoiceItemSearchResultDto>>.Success(
                Array.Empty<InvoiceItemSearchResultDto>()
            );

        // Batch-resolve unique tax codes — ISriCatalogResolver filters by IsActive
        var vatCodes = matches.Select(m => m.VatCode).OfType<string>().Distinct();
        var iceCodes = matches.Select(m => m.IceCode).OfType<string>().Distinct();

        var vatMap = await _sri.ResolveVatRatesAsync(vatCodes, cancellationToken);
        var iceMap = await _sri.ResolveIceRatesAsync(iceCodes, cancellationToken);

        // SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: 2 queries totales (lista default + sus reglas
        // activas), sin importar cuántos ítems trajo la búsqueda — nunca N+1 por resultado. Mismo
        // patrón que GetItemPricingSimulationQueryHandler. Si no hay lista default vigente, la
        // búsqueda sigue funcionando igual que antes (sin datos de descuento).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var defaultList = await _priceLists.GetDefaultAsync(_tenant.TenantId, cancellationToken);
        if (defaultList is not null && (!defaultList.IsActive || !defaultList.IsValidOn(today)))
            defaultList = null;
        var rulesByItemId = defaultList is null
            ? new Dictionary<Guid, PricingRule>()
            : (await _rules.GetByPriceListAsync(_tenant.TenantId, defaultList.Id, cancellationToken))
                .ToDictionary(r => r.ItemId);

        var results = matches
            .Select(m => Enrich(m, vatMap, iceMap, defaultList, rulesByItemId, _strategies))
            .ToList();
        return Result<IReadOnlyList<InvoiceItemSearchResultDto>>.Success(results);
    }

    private static InvoiceItemSearchResultDto Enrich(
        InvoiceItemMatch match,
        IReadOnlyDictionary<string, SriVatInfo> vatMap,
        IReadOnlyDictionary<string, SriIceInfo> iceMap,
        PriceList? defaultList,
        IReadOnlyDictionary<Guid, PricingRule> rulesByItemId,
        IPricingAdjustmentStrategyResolver strategies
    )
    {
        var vatInfo = !string.IsNullOrWhiteSpace(match.VatCode)
            ? vatMap.GetValueOrDefault(match.VatCode)
            : null;
        var iceInfo = !string.IsNullOrWhiteSpace(match.IceCode)
            ? iceMap.GetValueOrDefault(match.IceCode)
            : null;

        var vatPct = vatInfo?.Percent ?? 0m;
        var icePct = iceInfo?.Percent ?? 0m;

        var vatDisplay = vatInfo is not null
            ? vatPct == 0m
                ? "IVA 0%"
                : $"IVA {vatPct:0.##}%"
            : "Sin IVA";
        var iceDisplay = iceInfo is not null ? $"ICE {icePct:0.##}%" : "—";

        decimal? finalSalePrice = null;
        if (match.SalePriceWithoutTax.HasValue)
        {
            var (_, _, taxInclusive) = SriTaxCalculator.Compute(
                match.SalePriceWithoutTax.Value,
                vatPct,
                icePct
            );
            finalSalePrice = taxInclusive;
        }

        string? priceListName = null;
        string? discountDescription = null;
        decimal? discountedSalePriceWithoutTax = null;
        decimal? discountedFinalSalePrice = null;
        if (defaultList is not null && match.SalePriceWithoutTax.HasValue)
        {
            rulesByItemId.TryGetValue(match.Id, out var itemRule);
            var (netPrice, ruleApplied) = PricingCalculation.Resolve(
                match.SalePriceWithoutTax.Value,
                itemRule,
                defaultList,
                strategies
            );
            if (ruleApplied is not null)
            {
                priceListName = defaultList.Name;
                discountDescription = PricingCalculation.Summarize(itemRule, defaultList).Description;
                discountedSalePriceWithoutTax = netPrice;
                var (_, _, discountedTaxInclusive) = SriTaxCalculator.Compute(netPrice, vatPct, icePct);
                discountedFinalSalePrice = discountedTaxInclusive;
            }
        }

        return new InvoiceItemSearchResultDto(
            match.Id,
            match.Sku,
            match.Description,
            match.ProductFamilyName,
            match.UomAbbrev,
            match.TracksStock,
            match.WarehouseName,
            match.AvailableStock,
            match.AverageCost,
            match.SalePriceWithoutTax,
            finalSalePrice,
            vatDisplay,
            iceDisplay,
            match.VatCode,
            match.IceCode,
            match.BaseUomCode,
            match.PackagingLevels,
            match.MatchedPackagingLevelId,
            priceListName,
            discountDescription,
            discountedSalePriceWithoutTax,
            discountedFinalSalePrice
        );
    }
}
