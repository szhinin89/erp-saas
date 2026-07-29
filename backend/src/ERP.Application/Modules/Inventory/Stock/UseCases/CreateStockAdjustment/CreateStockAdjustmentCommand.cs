using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;

public sealed record CreateStockAdjustmentCommand(
    Guid WarehouseId,
    string WarehouseName,
    Guid ProductId,
    string ProductName,
    decimal AdjustmentQty,
    string Reason,
    string? Notes)
    : IRequest<Result<StockAdjustmentDto>>, IBranchScopedRequest;
