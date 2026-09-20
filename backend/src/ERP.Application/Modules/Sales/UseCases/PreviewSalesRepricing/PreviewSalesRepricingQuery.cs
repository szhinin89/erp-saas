using ERP.Application.Common;
using ERP.Application.Modules.Pricing.Services;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.PreviewSalesRepricing;

/// <summary>
/// SALES-CUSTOMER-REPRICING-PREVIEW-06C1: previsualiza cómo cambiaría el precio de las líneas
/// ya cargadas en un Draft si el cliente pasara de <see cref="OldCustomerId"/> a
/// <see cref="NewCustomerId"/> — SOLO lectura, no aplica ningún cambio. Sales nunca resuelve
/// listas de precios ni invoca PricingCalculation directamente: delega ambas resoluciones a
/// IPricingResolver.ResolveManyAsync (mismo SSOT que Create/Update Draft usan tras 06B), una vez
/// por cliente, nunca por ítem.
/// </summary>
public sealed record PreviewSalesRepricingQuery(
    IReadOnlyList<Guid> ItemIds,
    Guid? OldCustomerId,
    Guid? NewCustomerId
) : IRequest<Result<IReadOnlyList<SalesRepricingPreviewItemDto>>>, ICompanyScopedRequest;

/// <summary>
/// Comparación de precio resuelto para un ítem entre el cliente anterior y el nuevo. Los campos
/// "Old*"/"New*" reflejan exactamente lo que devolvió <see cref="Pricing.DTOs.PricingResult"/>
/// para cada cliente — ninguno se recalcula ni ajusta aquí.
/// </summary>
public sealed record SalesRepricingPreviewItemDto(
    Guid ItemId,
    decimal OldResolvedPrice,
    decimal NewResolvedPrice,
    bool Changed,
    Guid? OldPriceListId,
    string OldPriceListName,
    Guid? NewPriceListId,
    string NewPriceListName,
    PriceListSelectionSource? OldSelectionSource,
    PriceListSelectionSource? NewSelectionSource,
    string? NewDiscountDescription,
    // SALES-CUSTOMER-REPRICE-METADATA-06C2A: precio de lista ANTES de cualquier descuento
    // (PricingResult.BasePrice del cliente nuevo) — sin esto, el frontend no podía reconstruir
    // el mismo estado de línea que produce addLineWithItem (ver SalesItemPricingDto.basePrice,
    // mismo campo en el endpoint de pricing puntual).
    decimal NewBasePrice
);
