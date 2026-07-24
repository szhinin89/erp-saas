using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStock;

public sealed record GetStockQuery(Guid? ItemId, Guid? WarehouseId)
    : IRequest<Result<IReadOnlyList<CurrentStockDto>>>, IBranchScopedRequest;
