using FluentValidation;

namespace ERP.Application.Sales.UseCases.ValidarVenta;

public sealed class ValidarSaleCommandValidator : AbstractValidator<ValidarSaleCommand>
{
    public ValidarSaleCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
