using FluentValidation;

namespace ERP.Application.Modules.Compras.UseCases.VincularFacturaAOrdenCompra;

public sealed class VincularFacturaAOrdenCompraCommandValidator
    : AbstractValidator<VincularFacturaAOrdenCompraCommand>
{
    public VincularFacturaAOrdenCompraCommandValidator()
    {
        RuleFor(x => x.OrdenCompraId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");

        RuleFor(x => x.CompraFacturaId)
            .NotEmpty()
            .WithMessage("El ID de la factura de compra es obligatorio.");
    }
}
