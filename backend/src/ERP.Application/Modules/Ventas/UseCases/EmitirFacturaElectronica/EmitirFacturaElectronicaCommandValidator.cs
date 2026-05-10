using FluentValidation;

namespace ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;

public sealed class EmitirFacturaElectronicaCommandValidator : AbstractValidator<EmitirFacturaElectronicaCommand>
{
    public EmitirFacturaElectronicaCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
