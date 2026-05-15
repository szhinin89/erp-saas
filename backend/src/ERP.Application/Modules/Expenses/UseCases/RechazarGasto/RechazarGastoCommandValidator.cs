using FluentValidation;
using ERP.Domain.Modules.Expenses.Entities;

namespace ERP.Application.Modules.Expenses.UseCases.RechazarGasto;

public sealed class RechazarGastoCommandValidator : AbstractValidator<RechazarGastoCommand>
{
    public RechazarGastoCommandValidator()
    {
        RuleFor(x => x.ExpenseInvoiceId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(ExpenseInvoice.RejectionReasonMaxLen)
            .WithMessage($"El motivo no puede superar {ExpenseInvoice.RejectionReasonMaxLen} caracteres.");
    }
}
