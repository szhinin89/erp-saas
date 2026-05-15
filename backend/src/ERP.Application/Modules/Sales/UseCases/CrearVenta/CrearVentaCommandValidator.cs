using FluentValidation;

namespace ERP.Application.Sales.UseCases.CrearVenta;

public sealed class CrearVentaCommandValidator : AbstractValidator<CrearVentaCommand>
{
    public CrearVentaCommandValidator()
    {
        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("El ID del cliente es obligatorio.");

        RuleFor(x => x.BodegaId)
            .NotEmpty().WithMessage("El ID de la bodega es obligatorio.");

        RuleFor(x => x.SucursalId)
            .NotEmpty().WithMessage("El ID de la sucursal es obligatorio.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Debe incluir al menos un ítem en la venta.")
            .Must(items => items.Count > 0).WithMessage("Debe incluir al menos un ítem en la venta.");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductoId)
                    .NotEmpty().WithMessage("El ID del producto es obligatorio.");

                item.RuleFor(i => i.Cantidad)
                    .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

                item.RuleFor(i => i.PrecioUnitario)
                    .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
            });
    }
}