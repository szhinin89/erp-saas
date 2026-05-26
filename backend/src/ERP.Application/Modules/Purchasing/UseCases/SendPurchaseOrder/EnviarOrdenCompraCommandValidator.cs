using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.SendPurchaseOrder;

public sealed class EnviarOrderPurchaseCommandValidator : AbstractValidator<SendOrderPurchaseCommand>
{
    public EnviarOrderPurchaseCommandValidator()
    {
        RuleFor(x => x.OrdenId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");
    }
}
