using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.GetCurrentStockPorBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetCurrentStockPorWarehouseQuery(Guid    WarehouseId, Guid?     ProductId)
    : IRequest<Result<IReadOnlyList<CurrentStockListItemDto>>>, ICompanyScopedRequest;
