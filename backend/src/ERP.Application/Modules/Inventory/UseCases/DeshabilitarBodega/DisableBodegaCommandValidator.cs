using FluentValidation;

namespace ERP.Application.Modules.Inventory.UseCases.DeshabilitarBodega;

public sealed class DisableBodegaCommandValidator : AbstractValidator<DisableBodegaCommand>
{
    public DisableBodegaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la Warehouse es obligatorio.");
    }
}
