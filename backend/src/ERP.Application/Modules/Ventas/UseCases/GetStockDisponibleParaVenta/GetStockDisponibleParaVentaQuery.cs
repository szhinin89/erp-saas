using MediatR;
using ERP.Application.Common;
using ERP.Application.Ventas.DTOs;

namespace ERP.Application.Ventas.UseCases.GetStockDisponibleParaVenta;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetStockDisponibleParaVentaQuery(
    Guid ProductoId,
    Guid BodegaId
) : IRequest<Result<StockDisponibleDto>>;
