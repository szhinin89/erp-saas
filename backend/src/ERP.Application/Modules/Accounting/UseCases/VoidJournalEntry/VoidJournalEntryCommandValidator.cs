using FluentValidation;

namespace ERP.Application.Modules.Accounting.UseCases.VoidJournalEntry;

public sealed class VoidJournalEntryCommandValidator : AbstractValidator<VoidJournalEntryCommand>
{
    public VoidJournalEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del asiento es obligatorio.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de anulación es obligatorio.")
            .MaximumLength(500).WithMessage("El motivo no puede exceder 500 caracteres.");
    }
}
