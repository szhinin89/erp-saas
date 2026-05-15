using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.ValidarGasto;

public sealed class ValidarGastoCommandValidator : AbstractValidator<ValidarGastoCommand>
{
    public ValidarGastoCommandValidator()
    {
        RuleFor(x => x.GastoFacturaId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");
    }
}
