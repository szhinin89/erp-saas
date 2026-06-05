using FluentValidation;

namespace ERP.Application.Sales.UseCases.IssueElectronicInvoice;

public sealed class IssueElectronicInvoiceCommandValidator : AbstractValidator<IssueElectronicInvoiceCommand>
{
    public IssueElectronicInvoiceCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("El ID de la salesBill es obligatorio.");
    }
}
