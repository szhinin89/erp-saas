using FluentValidation;

namespace ERP.Application.Sales.UseCases.ValidateSale;

public sealed class ValidarSaleCommandValidator : AbstractValidator<ValidateSaleCommand>
{
    public ValidarSaleCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
