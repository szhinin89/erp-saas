using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockTransfer;

public sealed record TransferLineInput(Guid ProductId, decimal Quantity, string Description);

public sealed record CreateStockTransferCommand(
    Guid SourceWarehouseId,
    Guid TargetWarehouseId,
    string? Reason,
    string? Notes,
    IReadOnlyList<TransferLineInput> Lines
) : IRequest<Result<StockTransferDto>>, IInterBranchOperationRequest;
