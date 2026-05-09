using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;

namespace ERP.Application.Modules.Inventario.UseCases.GetStockActualPorBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetStockActualPorBodegaQuery(Guid BodegaId, Guid? ProductoId)
    : IRequest<Result<IReadOnlyList<StockActualListItemDto>>>;
