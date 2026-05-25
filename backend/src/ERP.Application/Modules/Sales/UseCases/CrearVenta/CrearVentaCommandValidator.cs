using FluentValidation;

namespace ERP.Application.Sales.UseCases.CrearVenta;

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        When(x => x.SalesOrderPublicId is null, () =>
        {
            RuleFor(x => x.BusinessPartnerId)
                .NotEmpty().WithMessage("El ID del cliente es obligatorio.");

            RuleFor(x => x.WarehouseId)
                .NotEmpty().WithMessage("El ID de la Warehouse es obligatorio.");

            RuleFor(x => x.BranchId)
                .NotEmpty().WithMessage("El ID de la sucursal es obligatorio.");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("Debe incluir al menos un ítem en la venta.")
                .Must(items => items.Count > 0).WithMessage("Debe incluir al menos un ítem en la venta.");

            RuleForEach(x => x.Items)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.ProductId)
                        .NotEmpty().WithMessage("El ID del producto es obligatorio.");

                    item.RuleFor(i => i.Quantity)
                        .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

                    item.RuleFor(i => i.UnitPrice)
                        .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
                });
        });

        When(x => x.SalesOrderPublicId is not null, () =>
        {
            RuleFor(x => x.SalesOrderPublicId)
                .NotEmpty().WithMessage("El ID del pedido es obligatorio.");
        });
    }
}
