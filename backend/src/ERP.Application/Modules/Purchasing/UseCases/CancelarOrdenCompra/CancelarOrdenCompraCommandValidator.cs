using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.CancelarOrdenCompra;

public sealed class CancelarOrdenCompraCommandValidator : AbstractValidator<CancelarOrdenCompraCommand>
{
    public CancelarOrdenCompraCommandValidator()
    {
        RuleFor(x => x.OrdenId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");
    }
}
