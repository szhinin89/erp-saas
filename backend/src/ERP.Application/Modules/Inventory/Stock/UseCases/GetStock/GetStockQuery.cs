using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStock;

public sealed record GetStockQuery(Guid? ItemId, Guid? WarehouseId)
    : IRequest<Result<IReadOnlyList<CurrentStockDto>>>,
        IBranchScopedRequest;
