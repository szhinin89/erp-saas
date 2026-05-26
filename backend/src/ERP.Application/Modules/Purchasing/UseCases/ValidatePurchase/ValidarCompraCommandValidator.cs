using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.ValidatePurchase;

public sealed class ValidarPurchaseCommandValidator : AbstractValidator<ValidatePurchaseCommand>
{
    public ValidarPurchaseCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
