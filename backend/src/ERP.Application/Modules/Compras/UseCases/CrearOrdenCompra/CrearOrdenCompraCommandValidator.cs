using FluentValidation;

namespace ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;

public sealed class CrearOrdenCompraCommandValidator : AbstractValidator<CrearOrdenCompraCommand>
{
    public CrearOrdenCompraCommandValidator()
    {
        RuleFor(x => x.ProveedorId)
            .NotEmpty().WithMessage("El proveedor es obligatorio.");

        RuleFor(x => x.FechaRequerida)
            .NotEmpty().WithMessage("La fecha requerida es obligatoria.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("La orden debe tener al menos un ítem.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductoId)
                .NotEmpty().WithMessage("El ID del producto es obligatorio.");

            item.RuleFor(i => i.Cantidad)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

            item.RuleFor(i => i.PrecioUnitario)
                .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");

            item.RuleFor(i => i.IvaPorcentaje)
                .GreaterThanOrEqualTo(0).WithMessage("El porcentaje de IVA no puede ser negativo.");
        });
    }
}
