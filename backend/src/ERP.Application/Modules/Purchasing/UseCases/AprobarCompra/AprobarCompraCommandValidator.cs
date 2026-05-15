using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;

public sealed class AprobarCompraCommandValidator : AbstractValidator<AprobarCompraCommand>
{
    public AprobarCompraCommandValidator()
    {
        RuleFor(x => x.CompraFacturaId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
