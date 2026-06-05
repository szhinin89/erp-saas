using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CancelAdjustment;

public sealed class CancelStockAdjustmentCommandValidator : AbstractValidator<CancelStockAdjustmentCommand>
{
    public CancelStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId)
            .NotEmpty()
            .WithMessage("El ID del ajuste es obligatorio.");
    }
}
