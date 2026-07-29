using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;

/// <summary>
/// Resuelve el precio e impuestos oficiales de un ítem en el momento en que se
/// selecciona para agregarlo como línea de venta (evento de selección puntual,
/// no el buscador batch de item-search). Delega a IPricingResolver (Pricing
/// Engine v2) — nunca calcula el precio localmente.
/// </summary>
public sealed record GetSalesItemPricingQuery(Guid ItemId)
    : IRequest<Result<SalesItemPricingDto>>, ICompanyScopedRequest;
