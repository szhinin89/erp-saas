using FluentValidation;

namespace ERP.Application.Inventory.UseCases.ConfirmarTransferencia;

public sealed class ConfirmTransferCommandValidator : AbstractValidator<ConfirmTransferCommand>
{
    public ConfirmTransferCommandValidator()
    {
        RuleFor(x => x.TransferId)
            .NotEmpty().WithMessage("El ID de la transfer es obligatorio.");
    }
}
