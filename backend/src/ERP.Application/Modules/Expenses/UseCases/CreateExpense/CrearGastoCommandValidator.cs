using FluentValidation;

namespace ERP.Application.Modules.Expenses.UseCases.CreateExpense;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        When(x => x.Modo == ExpenseCreationMode.Xml, () =>
        {
            RuleFor(x => x.XmlContent)
                .NotNull().WithMessage("El XML es obligatorio en modo XML.")
                .Must(b => b is { Length: > 0 }).WithMessage("El archivo XML no puede estar vacío.");

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("La categoría de gasto es obligatoria en modo XML.")
                .MaximumLength(ERP.Domain.Modules.Expenses.Entities.ExpenseInvoice.CategoryMaxLen);
        });

        When(x => x.Modo == ExpenseCreationMode.Manual, () =>
        {
            RuleFor(x => x.IssueDate)
                .NotNull().WithMessage("La fecha de emisión es obligatoria en modo manual.");

            RuleFor(x => x.Concept)
                .NotEmpty().WithMessage("El concepto es obligatorio en modo manual.")
                .MaximumLength(ERP.Domain.Modules.Expenses.Entities.ExpenseInvoice.ConceptMaxLen);

            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("La categoría de gasto es obligatoria en modo manual.")
                .MaximumLength(ERP.Domain.Modules.Expenses.Entities.ExpenseInvoice.CategoryMaxLen);

            RuleFor(x => x.Subtotal).NotNull().WithMessage("El subtotal es obligatorio en modo manual.");
            RuleFor(x => x.VatTotal).NotNull().WithMessage("El impuesto es obligatorio en modo manual.");
            RuleFor(x => x.Total).NotNull().WithMessage("El total es obligatorio en modo manual.");

            RuleFor(x => x.Subtotal!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("El subtotal no puede ser negativo.");
            RuleFor(x => x.VatTotal!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("El impuesto no puede ser negativo.");
            RuleFor(x => x.Total!.Value)
                .GreaterThan(0).WithMessage("El total debe ser mayor a cero.");
        });
    }
}
