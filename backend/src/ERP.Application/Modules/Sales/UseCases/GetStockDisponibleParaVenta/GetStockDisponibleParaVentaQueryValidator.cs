using FluentValidation;

namespace ERP.Application.Sales.UseCases.GetStockDisponibleParaVenta;

public sealed class GetStockDisponibleParaVentaQueryValidator : AbstractValidator<GetStockDisponibleParaVentaQuery>
{
    public GetStockDisponibleParaVentaQueryValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El ID del producto es obligatorio.");

        RuleFor(x => x.BodegaId)
            .NotEmpty().WithMessage("El ID de la bodega es obligatorio.");
    }
}
