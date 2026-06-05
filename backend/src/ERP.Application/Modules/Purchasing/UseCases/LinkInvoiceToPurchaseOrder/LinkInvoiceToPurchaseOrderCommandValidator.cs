using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.LinkInvoiceToPurchaseOrder;

public sealed class LinkInvoiceToPurchaseOrderCommandValidator
    : AbstractValidator<LinkInvoiceToPurchaseOrderCommand>
{
    public LinkInvoiceToPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");

        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la salesBill de compra es obligatorio.");
    }
}
