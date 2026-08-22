using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Common;
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
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public SearchItemsForInvoiceHandler(
        IInvoiceItemSearchRepository repo,
        ISriCatalogResolver sri,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _repo = repo;
        _sri = sri;
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

        var results = matches.Select(m => Enrich(m, vatMap, iceMap)).ToList();
        return Result<IReadOnlyList<InvoiceItemSearchResultDto>>.Success(results);
    }

    private static InvoiceItemSearchResultDto Enrich(
        InvoiceItemMatch match,
        IReadOnlyDictionary<string, SriVatInfo> vatMap,
        IReadOnlyDictionary<string, SriIceInfo> iceMap
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
            match.MatchedPackagingLevelId
        );
    }
}
