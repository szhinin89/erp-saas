using FluentValidation;

namespace ERP.Application.Sales.UseCases.VoidInvoice;

public sealed class VoidInvoiceCommandValidator : AbstractValidator<VoidInvoiceCommand>
{
    public VoidInvoiceCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("El ID de la salesBill es obligatorio.");
    }
}
