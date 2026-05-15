using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.AprobarGasto;

public sealed class AprobarGastoCommandValidator : AbstractValidator<AprobarGastoCommand>
{
    public AprobarGastoCommandValidator()
    {
        RuleFor(x => x.ExpenseInvoiceId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");
    }
}
