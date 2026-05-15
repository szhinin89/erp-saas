using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarOrdenCompra;

public sealed class AprobarOrderPurchaseCommandValidator : AbstractValidator<AprobarOrderPurchaseCommand>
{
    public AprobarOrderPurchaseCommandValidator()
    {
        RuleFor(x => x.OrdenId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");
    }
}
