using FluentValidation;

namespace ERP.Application.Sales.UseCases.GetStockDisponibleParaVenta;

public sealed class GetStockDisponibleParaVentaQueryValidator : AbstractValidator<GetStockDisponibleParaVentaQuery>
{
    public GetStockDisponibleParaVentaQueryValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("El ID del producto es obligatorio.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("El ID de la Warehouse es obligatorio.");
    }
}
