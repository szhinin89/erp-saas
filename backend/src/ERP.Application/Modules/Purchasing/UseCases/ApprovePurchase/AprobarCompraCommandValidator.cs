using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.ApprovePurchase;

public sealed class AprobarPurchaseCommandValidator : AbstractValidator<ApprovePurchaseCommand>
{
    public AprobarPurchaseCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
