using FluentValidation;

namespace ERP.Application.Items.UseCases.UpdateItem;

public sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El Id del ítem es obligatorio.");

        RuleFor(x => x.ShortName)
            .NotEmpty().WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre corto no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(254).WithMessage("La descripción no puede exceder 254 caracteres.");

        RuleFor(x => x.DefaultUomCode)
            .NotEmpty().WithMessage("La unidad de medida base es obligatoria.")
            .MaximumLength(10).WithMessage("El código UOM no puede exceder 10 caracteres.");

        RuleFor(x => x.SaleVatCode)
            .NotEmpty().WithMessage("Debe especificar el código de IVA para venta.")
            .When(x => x.AppliesVatOnSale);

        RuleFor(x => x.PurchaseVatCode)
            .NotEmpty().WithMessage("Debe especificar el código de IVA para compra.")
            .When(x => x.AppliesVatOnPurchase);

        RuleFor(x => x.ExciseTaxCode)
            .NotEmpty().WithMessage("Debe especificar el código de ICE.")
            .When(x => x.AppliesExciseTax);

        RuleFor(x => x.MaxDiscountPercent)
            .InclusiveBetween(0, 100).WithMessage("El descuento máximo debe estar entre 0 y 100.")
            .When(x => x.MaxDiscountPercent.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MinStockQty.HasValue || !x.MaxStockQty.HasValue || x.MinStockQty <= x.MaxStockQty)
            .WithMessage("El stock mínimo no puede ser mayor que el máximo.")
            .WithName("StockQty");
    }
}
