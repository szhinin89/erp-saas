using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.ValidateExpense;

public sealed class ValidateExpenseCommandValidator : AbstractValidator<ValidateExpenseCommand>
{
    public ValidateExpenseCommandValidator()
    {
        RuleFor(x => x.ExpenseInvoiceId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");
    }
}
