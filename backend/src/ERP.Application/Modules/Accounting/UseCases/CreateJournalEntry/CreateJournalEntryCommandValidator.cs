using FluentValidation;

namespace ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;

public sealed class CreateJournalEntryCommandValidator : AbstractValidator<CreateJournalEntryCommand>
{
    public CreateJournalEntryCommandValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("La referencia del asiento es obligatoria.")
            .MaximumLength(50).WithMessage("La referencia no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción del asiento es obligatoria.")
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("La fecha del asiento es obligatoria.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("El asiento debe tener al menos una línea.")
            .Must(lines => lines.Count >= 2)
            .WithMessage("El asiento debe tener al menos dos líneas contables.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId)
                .NotEmpty().WithMessage("El ID de cuenta en cada línea es obligatorio.");

            line.RuleFor(l => l.Currency)
                .NotEmpty().WithMessage("La moneda es obligatoria.")
                .MaximumLength(3).WithMessage("El código de moneda no puede exceder 3 caracteres.");

            line.RuleFor(l => l.DebitAmount)
                .GreaterThanOrEqualTo(0).WithMessage("El monto de débito no puede ser negativo.");

            line.RuleFor(l => l.CreditAmount)
                .GreaterThanOrEqualTo(0).WithMessage("El monto de crédito no puede ser negativo.");

            line.RuleFor(l => l)
                .Must(l => l.DebitAmount > 0 || l.CreditAmount > 0)
                .WithMessage("Cada línea debe tener débito o crédito mayor a cero.");
        });
    }
}
