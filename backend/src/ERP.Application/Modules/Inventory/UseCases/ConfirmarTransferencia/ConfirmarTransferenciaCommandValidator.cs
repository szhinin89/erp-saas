using FluentValidation;

namespace ERP.Application.Inventory.UseCases.ConfirmarTransferencia;

public sealed class ConfirmarTransferenciaCommandValidator : AbstractValidator<ConfirmarTransferenciaCommand>
{
    public ConfirmarTransferenciaCommandValidator()
    {
        RuleFor(x => x.TransferenciaId)
            .NotEmpty().WithMessage("El ID de la transfer es obligatorio.");
    }
}
