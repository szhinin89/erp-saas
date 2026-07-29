using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements;

public sealed record GetStockMovementsQuery(
    Guid ItemId,
    Guid WarehouseId,
    DateTime? From,
    DateTime? To)
    : IRequest<Result<IReadOnlyList<StockMovementDto>>>, IBranchScopedRequest;
