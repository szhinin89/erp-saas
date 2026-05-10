using FluentValidation;

namespace ERP.Application.Inventario.UseCases.ConfirmarTransferencia;

public sealed class ConfirmarTransferenciaCommandValidator : AbstractValidator<ConfirmarTransferenciaCommand>
{
    public ConfirmarTransferenciaCommandValidator()
    {
        RuleFor(x => x.TransferenciaId)
            .NotEmpty().WithMessage("El ID de la transferencia es obligatorio.");
    }
}
