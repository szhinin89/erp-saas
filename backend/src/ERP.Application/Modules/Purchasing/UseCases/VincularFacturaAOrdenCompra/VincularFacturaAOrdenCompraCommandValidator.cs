using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;

public sealed class VincularFacturaAOrderPurchaseCommandValidator
    : AbstractValidator<VincularFacturaAOrderPurchaseCommand>
{
    public VincularFacturaAOrderPurchaseCommandValidator()
    {
        RuleFor(x => x.OrdenCompraId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");

        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
