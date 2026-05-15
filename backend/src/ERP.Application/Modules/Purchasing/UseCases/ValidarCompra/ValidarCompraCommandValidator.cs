using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;

public sealed class ValidarCompraCommandValidator : AbstractValidator<ValidarCompraCommand>
{
    public ValidarCompraCommandValidator()
    {
        RuleFor(x => x.PurchBillId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
