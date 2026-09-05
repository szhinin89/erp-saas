using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStockAdjustment;

public sealed record GetStockAdjustmentByIdQuery(Guid Id)
    : IRequest<Result<StockAdjustmentDto>>, IBranchScopedRequest;
