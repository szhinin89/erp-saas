using FluentValidation;
using ERP.Domain.Modules.Gastos.Entities;

namespace ERP.Application.Modules.Gastos.UseCases.RechazarGasto;

public sealed class RechazarGastoCommandValidator : AbstractValidator<RechazarGastoCommand>
{
    public RechazarGastoCommandValidator()
    {
        RuleFor(x => x.GastoFacturaId)
            .NotEmpty()
            .WithMessage("El ID del gasto es obligatorio.");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(GastoFactura.MotivoRechazoMaxLen)
            .WithMessage($"El motivo no puede superar {GastoFactura.MotivoRechazoMaxLen} caracteres.");
    }
}
