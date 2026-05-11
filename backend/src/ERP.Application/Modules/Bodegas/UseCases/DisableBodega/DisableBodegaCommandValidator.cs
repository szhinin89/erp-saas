using FluentValidation;

namespace ERP.Application.Modules.Bodegas.UseCases.DisableBodega;

public sealed class DisableBodegaCommandValidator : AbstractValidator<DisableBodegaCommand>
{
    public DisableBodegaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la bodega es obligatorio.");
    }
}
