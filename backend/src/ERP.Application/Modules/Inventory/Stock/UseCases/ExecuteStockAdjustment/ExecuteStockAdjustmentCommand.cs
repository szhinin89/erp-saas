using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;

public sealed record ExecuteStockAdjustmentCommand(Guid Id)
    : IRequest<Result<StockAdjustmentDto>>,
        IBranchScopedRequest;

public sealed class ExecuteStockAdjustmentValidator : AbstractValidator<ExecuteStockAdjustmentCommand>
{
    public ExecuteStockAdjustmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
