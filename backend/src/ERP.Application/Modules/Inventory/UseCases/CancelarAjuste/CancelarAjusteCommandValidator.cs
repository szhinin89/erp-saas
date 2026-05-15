using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CancelarAjuste;

public sealed class CancelStockAdjustmentCommandValidator : AbstractValidator<CancelStockAdjustmentCommand>
{
    public CancelStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId)
            .NotEmpty()
            .WithMessage("El ID del ajuste es obligatorio.");
    }
}
