using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;

public sealed record CancelStockAdjustmentCommand(Guid Id, string Reason)
    : IRequest<Result<StockAdjustmentDto>>,
        IBranchScopedRequest;

public sealed class CancelStockAdjustmentValidator : AbstractValidator<CancelStockAdjustmentCommand>
{
    public CancelStockAdjustmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(StockAdjustment.CancelledReasonMaxLen)
            .WithMessage("El motivo de anulación es obligatorio (máximo 500 caracteres).");
    }
}
