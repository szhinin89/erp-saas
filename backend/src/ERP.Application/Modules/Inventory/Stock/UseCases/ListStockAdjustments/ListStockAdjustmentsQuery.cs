using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ListStockAdjustments;

public sealed record ListStockAdjustmentsQuery(
    Guid? WarehouseId,
    string? Status,
    Guid? ReasonId,
    string? MovementType,
    DateTime? StartDate,
    DateTime? EndDate,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<StockAdjustmentDto>>>, IBranchScopedRequest;
