using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;

public sealed class CrearOrdenCompraCommandValidator : AbstractValidator<CrearOrdenCompraCommand>
{
    public CrearOrdenCompraCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("El Supplier es obligatorio.");

        RuleFor(x => x.RequiredDate)
            .NotEmpty().WithMessage("La fecha requerida es obligatoria.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("La orden debe tener al menos un ítem.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("El ID del producto es obligatorio.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");

            item.RuleFor(i => i.VatPct)
                .GreaterThanOrEqualTo(0).WithMessage("El porcentaje de IVA no puede ser negativo.");
        });
    }
}
