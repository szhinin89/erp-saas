using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Domain.Common;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.PreviewSalesRepricing;

public sealed class PreviewSalesRepricingQueryHandler
    : IRequestHandler<PreviewSalesRepricingQuery, Result<IReadOnlyList<SalesRepricingPreviewItemDto>>>
{
    private readonly IPricingResolver _pricing;

    public PreviewSalesRepricingQueryHandler(IPricingResolver pricing) => _pricing = pricing;

    public async Task<Result<IReadOnlyList<SalesRepricingPreviewItemDto>>> Handle(
        PreviewSalesRepricingQuery request,
        CancellationToken ct
    )
    {
        var itemIds = request.ItemIds.Distinct().ToList();
        if (itemIds.Count == 0)
            return Result<IReadOnlyList<SalesRepricingPreviewItemDto>>.Success(
                Array.Empty<SalesRepricingPreviewItemDto>()
            );

        // Exactamente 2 llamadas batch (viejo cliente, nuevo cliente), nunca una por ítem —
        // mismo IPricingResolver.ResolveManyAsync que ya usa SalesLineBuilder (06B). Sales no
        // resuelve listas ni invoca PricingCalculation: solo compone dos resultados ya resueltos.
        var oldPricing = await _pricing.ResolveManyAsync(
            new PricingBatchContext(itemIds, request.OldCustomerId),
            ct
        );
        if (!oldPricing.IsSuccess)
            return Result<IReadOnlyList<SalesRepricingPreviewItemDto>>.ValidationFailure(
                oldPricing.Error!
            );

        var newPricing = await _pricing.ResolveManyAsync(
            new PricingBatchContext(itemIds, request.NewCustomerId),
            ct
        );
        if (!newPricing.IsSuccess)
            return Result<IReadOnlyList<SalesRepricingPreviewItemDto>>.ValidationFailure(
                newPricing.Error!
            );

        var oldByItem = oldPricing.Value!;
        var newByItem = newPricing.Value!;

        var items = new List<SalesRepricingPreviewItemDto>();
        foreach (var itemId in itemIds)
        {
            // Un ítem sin BaseSalePrice simplemente no aparece en ninguno de los dos diccionarios
            // (ResolveManyAsync ya lo excluye) — sin precio no hay nada que previsualizar.
            if (
                !oldByItem.TryGetValue(itemId, out var oldResult)
                || !newByItem.TryGetValue(itemId, out var newResult)
            )
                continue;

            var oldPrice = Round(oldResult.UnitPrice);
            var newPrice = Round(newResult.UnitPrice);

            items.Add(
                new SalesRepricingPreviewItemDto(
                    itemId,
                    oldPrice,
                    newPrice,
                    Changed: oldPrice != newPrice,
                    oldResult.PriceListId,
                    oldResult.PriceListName,
                    newResult.PriceListId,
                    newResult.PriceListName,
                    oldResult.SelectionSource,
                    newResult.SelectionSource,
                    newResult.RuleDescription
                )
            );
        }

        return Result<IReadOnlyList<SalesRepricingPreviewItemDto>>.Success(items);
    }

    // Misma precisión/regla que Sales/Pricing ya usan para comparar precios monetarios (ver
    // SalesLineBuilder.BuildAsync — ListPriceAtSale/UnitPrice, FiscalPrecision.UnitCost = 6
    // decimales, redondeo AwayFromZero) — evita falsos "Changed=true" por ruido de precisión.
    private static decimal Round(decimal value) =>
        Math.Round(value, FiscalPrecision.UnitCost, MidpointRounding.AwayFromZero);
}
