using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.ApprovePurchase;

public sealed class ApprovePurchaseCommandValidator : AbstractValidator<ApprovePurchaseCommand>
{
    public ApprovePurchaseCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la salesBill de compra es obligatorio.");
    }
}
