using FluentValidation;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed class CreateSalesNoteCommandValidator : AbstractValidator<CreateSalesNoteCommand>
{
    private static readonly HashSet<string> AllowedNoteTypes =
        new(StringComparer.OrdinalIgnoreCase) { "CREDIT", "CREDITO", "DEBIT", "DEBITO" };

    public CreateSalesNoteCommandValidator()
    {
        RuleFor(x => x.OriginalBillId).NotEmpty();
        RuleFor(x => x.NoteType)
            .NotEmpty()
            .Must(t => AllowedNoteTypes.Contains(t.Trim()))
            .WithMessage("Tipo de nota inválido. Use CREDIT o DEBIT.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty().WithMessage("La nota debe tener al menos una línea.");
        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(i => i.ProductId).NotEmpty();
            line.RuleFor(i => i.Quantity).GreaterThan(0);
            line.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
