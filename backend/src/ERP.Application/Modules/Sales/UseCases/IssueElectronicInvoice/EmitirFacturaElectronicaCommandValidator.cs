using FluentValidation;

namespace ERP.Application.Sales.UseCases.IssueElectronicInvoice;

public sealed class IssueElectronicInvoiceCommandValidator : AbstractValidator<IssueElectronicInvoiceCommand>
{
    public IssueElectronicInvoiceCommandValidator()
    {
        RuleFor(x => x.VentaId)
            .NotEmpty().WithMessage("El ID de la factura es obligatorio.");
    }
}
