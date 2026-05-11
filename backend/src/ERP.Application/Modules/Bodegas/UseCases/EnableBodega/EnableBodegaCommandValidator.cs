using FluentValidation;

namespace ERP.Application.Modules.Bodegas.UseCases.EnableBodega;

public sealed class EnableBodegaCommandValidator : AbstractValidator<EnableBodegaCommand>
{
    public EnableBodegaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la bodega es obligatorio.");
    }
}
