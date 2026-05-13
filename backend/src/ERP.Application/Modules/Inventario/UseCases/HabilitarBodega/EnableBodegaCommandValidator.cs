using FluentValidation;

namespace ERP.Application.Modules.Inventario.UseCases.HabilitarBodega;

public sealed class EnableBodegaCommandValidator : AbstractValidator<EnableBodegaCommand>
{
    public EnableBodegaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la bodega es obligatorio.");
    }
}
