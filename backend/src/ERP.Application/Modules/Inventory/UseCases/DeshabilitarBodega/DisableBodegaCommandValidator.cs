using FluentValidation;

namespace ERP.Application.Modules.Inventory.UseCases.DeshabilitarBodega;

public sealed class DisableWarehouseCommandValidator : AbstractValidator<DisableWarehouseCommand>
{
    public DisableWarehouseCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la Warehouse es obligatorio.");
    }
}
