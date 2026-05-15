using FluentValidation;

namespace ERP.Application.Sales.UseCases.ValidarVenta;

public sealed class ValidarVentaCommandValidator : AbstractValidator<ValidarVentaCommand>
{
    public ValidarVentaCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
