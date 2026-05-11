using FluentValidation;

namespace ERP.Application.Modules.Compras.UseCases.EnviarOrdenCompra;

public sealed class EnviarOrdenCompraCommandValidator : AbstractValidator<EnviarOrdenCompraCommand>
{
    public EnviarOrdenCompraCommandValidator()
    {
        RuleFor(x => x.OrdenId)
            .NotEmpty()
            .WithMessage("El ID de la orden de compra es obligatorio.");
    }
}
