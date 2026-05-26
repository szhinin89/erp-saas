using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CancelTransfer;

public sealed class CancelTransferCommandValidator : AbstractValidator<CancelTransferCommand>
{
    public CancelTransferCommandValidator()
    {
        RuleFor(x => x.TransferId)
            .NotEmpty()
            .WithMessage("El ID de la transfer es obligatorio.");
    }
}
