using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;

/// <summary>
/// Resuelve el precio e impuestos oficiales de un ítem en el momento en que se
/// selecciona para agregarlo como línea de venta (evento de selección puntual,
/// no el buscador batch de item-search). Delega a IPricingResolver (Pricing
/// Engine v2) — nunca calcula el precio localmente.
///
/// SALES-CONTEXTUAL-PRICING-READ-06A: <see cref="CustomerId"/> es opcional — Sales nunca decide
/// qué PriceList corresponde ni consulta PriceListCustomer/PriceListItem directamente, solo
/// informa el cliente actual y deja que IPricingResolver resuelva Customer → CompanyDefault →
/// PVP. Sin cliente (o cliente sin lista propia), el comportamiento es idéntico al de antes.
/// </summary>
public sealed record GetSalesItemPricingQuery(Guid ItemId, Guid? CustomerId = null)
    : IRequest<Result<SalesItemPricingDto>>,
        ICompanyScopedRequest;
