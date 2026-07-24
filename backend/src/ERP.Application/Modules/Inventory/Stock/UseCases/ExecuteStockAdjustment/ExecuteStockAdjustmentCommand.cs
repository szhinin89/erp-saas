using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;

public sealed record ExecuteStockAdjustmentCommand(Guid Id)
    : IRequest<Result<StockAdjustmentDto>>, IBranchScopedRequest;
