using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.AprobarGasto;

public sealed class AprobarGastoCommandValidator : AbstractValidator<ApproveExpenseCommand>
{
    public AprobarGastoCommandValidator()
    {
        RuleFor(x => x.ExpenseInvoiceId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");
    }
}
