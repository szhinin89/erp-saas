using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Common;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

/// <summary>
/// SALES-CONTEXTUAL-PRICING-READ-06A: <see cref="CustomerId"/> es opcional — Sales solo informa
/// el cliente actualmente seleccionado (o null); nunca decide qué PriceList corresponde ni
/// consulta PriceListCustomer/PriceListItem/PricingRule directamente. Toda esa resolución vive
/// en IPricingResolver (Customer → CompanyDefault → PVP).
/// </summary>
public sealed record SearchItemsForInvoiceQuery(
    string Query,
    Guid? WarehouseId,
    int PageSize = 10,
    Guid? CustomerId = null
) : IRequest<Result<IReadOnlyList<InvoiceItemSearchResultDto>>>, IBranchScopedRequest;

public sealed class SearchItemsForInvoiceHandler
    : IRequestHandler<SearchItemsForInvoiceQuery, Result<IReadOnlyList<InvoiceItemSearchResultDto>>>
{
    private readonly IInvoiceItemSearchRepository _repo;
    private readonly ISriCatalogResolver _sri;
    private readonly IPricingResolver _pricingResolver;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public SearchItemsForInvoiceHandler(
        IInvoiceItemSearchRepository repo,
        ISriCatalogResolver sri,
        IPricingResolver pricingResolver,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _sri = sri;
        _pricingResolver = pricingResolver;
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

        // SALES-CONTEXTUAL-PRICING-READ-06A: una sola llamada batch al Pricing Engine para TODOS
        // los ítems del resultado — Sales ya NO resuelve PriceList default, PriceListItem ni
        // PricingRule por su cuenta (eso vivía aquí antes, ver SALES-ITEM-SEARCH-PRICELIST-
        // ASSIGNMENT-AUDIT-03/PRICING-LIST-ASSIGNMENT-ENFORCEMENT-02). ResolveManyAsync ya
        // documenta su propio conteo de queries (~2 + 3×candidatos, nunca por ítem) — sin
        // importar cuántos resultados traiga la búsqueda.
        var pricingResult = await _pricingResolver.ResolveManyAsync(
            new PricingBatchContext(matches.Select(m => m.Id).ToList(), request.CustomerId),
            cancellationToken
        );
        if (!pricingResult.IsSuccess)
            return Result<IReadOnlyList<InvoiceItemSearchResultDto>>.ValidationFailure(
                pricingResult.Error!
            );
        var pricingByItemId = pricingResult.Value!;

        var results = matches
            .Select(m => Enrich(m, vatMap, iceMap, pricingByItemId))
            .ToList();
        return Result<IReadOnlyList<InvoiceItemSearchResultDto>>.Success(results);
    }

    private static InvoiceItemSearchResultDto Enrich(
        InvoiceItemMatch match,
        IReadOnlyDictionary<string, SriVatInfo> vatMap,
        IReadOnlyDictionary<string, SriIceInfo> iceMap,
        IReadOnlyDictionary<Guid, PricingResult> pricingByItemId
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

        // SALES-CONTEXTUAL-PRICING-READ-06A: "Promo" solo se muestra cuando el PricingResult
        // trae una regla realmente aplicada (excepción o regla general con efecto) — mismo
        // criterio que antes (RuleApplied != null), ahora decidido por IPricingResolver, nunca
        // recalculado aquí. Sin entrada en pricingByItemId (ítem sin BaseSalePrice) o con
        // RuleApplied null (PVP o lista sin ajuste), no hay promo — comportamiento sin cambios.
        string? priceListName = null;
        string? discountDescription = null;
        decimal? discountedSalePriceWithoutTax = null;
        decimal? discountedFinalSalePrice = null;
        if (pricingByItemId.TryGetValue(match.Id, out var pricing) && pricing.RuleApplied is not null)
        {
            priceListName = pricing.PriceListName;
            discountDescription = pricing.RuleDescription;
            discountedSalePriceWithoutTax = pricing.UnitPrice;
            var (_, _, discountedTaxInclusive) = SriTaxCalculator.Compute(pricing.UnitPrice, vatPct, icePct);
            discountedFinalSalePrice = discountedTaxInclusive;
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
