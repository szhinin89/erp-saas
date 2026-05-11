using FluentValidation;

namespace ERP.Application.Inventario.UseCases.CancelarTransferencia;

public sealed class CancelarTransferenciaCommandValidator : AbstractValidator<CancelarTransferenciaCommand>
{
    public CancelarTransferenciaCommandValidator()
    {
        RuleFor(x => x.TransferenciaId)
            .NotEmpty()
            .WithMessage("El ID de la transferencia es obligatorio.");
    }
}
