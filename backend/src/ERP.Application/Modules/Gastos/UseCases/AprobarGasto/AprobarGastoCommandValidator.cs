using FluentValidation;

namespace ERP.Application.Modules.Gastos.UseCases.AprobarGasto;

public sealed class AprobarGastoCommandValidator : AbstractValidator<AprobarGastoCommand>
{
    public AprobarGastoCommandValidator()
    {
        RuleFor(x => x.GastoFacturaId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");
    }
}
