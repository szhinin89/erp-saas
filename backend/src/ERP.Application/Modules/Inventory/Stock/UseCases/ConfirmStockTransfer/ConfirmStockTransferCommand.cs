using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ConfirmStockTransfer;

public sealed record ConfirmStockTransferCommand(Guid Id)
    : IRequest<Result<StockTransferDto>>, IInterBranchOperationRequest;
