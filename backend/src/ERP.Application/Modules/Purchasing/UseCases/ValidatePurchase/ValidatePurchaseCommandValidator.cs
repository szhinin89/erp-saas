using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.ValidatePurchase;

public sealed class ValidatePurchaseCommandValidator : AbstractValidator<ValidatePurchaseCommand>
{
    public ValidatePurchaseCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la salesBill de compra es obligatorio.");
    }
}
