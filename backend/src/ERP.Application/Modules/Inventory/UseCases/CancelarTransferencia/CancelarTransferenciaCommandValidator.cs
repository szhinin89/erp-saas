using FluentValidation;

namespace ERP.Application.Inventory.UseCases.CancelarTransferencia;

public sealed class CancelTransferCommandValidator : AbstractValidator<CancelTransferCommand>
{
    public CancelTransferCommandValidator()
    {
        RuleFor(x => x.TransferId)
            .NotEmpty()
            .WithMessage("El ID de la transfer es obligatorio.");
    }
}
