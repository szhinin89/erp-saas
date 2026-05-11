using FluentValidation;

namespace ERP.Application.Modules.Inventario.UseCases.DeshabilitarBodega;

public sealed class DisableBodegaCommandValidator : AbstractValidator<DisableBodegaCommand>
{
    public DisableBodegaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la bodega es obligatorio.");
    }
}
