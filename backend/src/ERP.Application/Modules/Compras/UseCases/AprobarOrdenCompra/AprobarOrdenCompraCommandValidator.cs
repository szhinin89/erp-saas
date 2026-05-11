using FluentValidation;

namespace ERP.Application.Modules.Compras.UseCases.AprobarOrdenCompra;

public sealed class AprobarOrdenCompraCommandValidator : AbstractValidator<AprobarOrdenCompraCommand>
{
    public AprobarOrdenCompraCommandValidator()
    {
        RuleFor(x => x.OrdenId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");
    }
}
