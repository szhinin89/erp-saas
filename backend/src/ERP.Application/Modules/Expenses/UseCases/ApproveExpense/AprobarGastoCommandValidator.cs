using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.ApproveExpense;

public sealed class ApproveExpenseCommandValidator : AbstractValidator<ApproveExpenseCommand>
{
    public ApproveExpenseCommandValidator()
    {
        RuleFor(x => x.ExpenseInvoiceId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");
    }
}
