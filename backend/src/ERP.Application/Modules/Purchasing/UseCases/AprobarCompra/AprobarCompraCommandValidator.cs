using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;

public sealed class AprobarPurchaseCommandValidator : AbstractValidator<AprobarPurchaseCommand>
{
    public AprobarPurchaseCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
