using FluentValidation;

namespace ERP.Application.Inventory.UseCases.ExecuteAdjustment;

public sealed class ExecuteStockAdjustmentCommandValidator : AbstractValidator<ExecuteStockAdjustmentCommand>
{
    public ExecuteStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId)
            .NotEmpty()
            .WithMessage("El ID del ajuste es obligatorio.");
    }
}
