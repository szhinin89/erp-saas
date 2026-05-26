using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.CancelPurchaseOrder;

public sealed class CancelarOrderPurchaseCommandValidator : AbstractValidator<CancelOrderPurchaseCommand>
{
    public CancelarOrderPurchaseCommandValidator()
    {
        RuleFor(x => x.OrdenId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");
    }
}
