using FluentValidation;

namespace ERP.Application.Modules.Compras.UseCases.ValidarCompra;

public sealed class ValidarCompraCommandValidator : AbstractValidator<ValidarCompraCommand>
{
    public ValidarCompraCommandValidator()
    {
        RuleFor(x => x.CompraFacturaId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
