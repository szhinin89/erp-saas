using FluentValidation;

namespace ERP.Application.Ventas.UseCases.ReintentarEnvio;

public sealed class ReintentarEnvioCommandValidator : AbstractValidator<ReintentarEnvioCommand>
{
    public ReintentarEnvioCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
