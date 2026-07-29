using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;

public sealed record ExecuteStockAdjustmentCommand(Guid Id)
    : IRequest<Result<StockAdjustmentDto>>,
        IBranchScopedRequest;
