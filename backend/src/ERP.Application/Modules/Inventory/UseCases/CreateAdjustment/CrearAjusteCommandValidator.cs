using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CreateAdjustment;

public sealed class CreateStockAdjustmentCommandValidator : AbstractValidator<CreateStockAdjustmentCommand>
{
    public CreateStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("La Warehouse es obligatoria.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("El producto es obligatorio.");

        RuleFor(x => x.AdjustmentQty)
            .NotEqual(0).WithMessage("La cantidad de ajuste no puede ser cero.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(200).WithMessage("El motivo no puede superar 200 caracteres.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).When(x => x.Notes is not null);
    }
}
