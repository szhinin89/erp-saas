using FluentValidation;

namespace ERP.Application.Modules.Inventory.UseCases.HabilitarBodega;

public sealed class EnableWarehouseCommandValidator : AbstractValidator<EnableWarehouseCommand>
{
    public EnableWarehouseCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la Warehouse es obligatorio.");
    }
}
