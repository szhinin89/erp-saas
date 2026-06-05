using FluentValidation;

namespace ERP.Application.Sales.UseCases.ValidateSale;

public sealed class ValidateSaleCommandValidator : AbstractValidator<ValidateSaleCommand>
{
    public ValidateSaleCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("El ID de la salesBill es obligatorio.");
    }
}
