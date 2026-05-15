using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.GetStockActualPorBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetStockActualPorBodegaQuery(Guid BodegaId, Guid? ProductoId)
    : IRequest<Result<IReadOnlyList<StockActualListItemDto>>>;
