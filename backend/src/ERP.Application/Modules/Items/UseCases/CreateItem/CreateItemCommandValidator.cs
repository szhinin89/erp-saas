using FluentValidation;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.Application.Items.UseCases.CreateItem;

public sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("El SKU es obligatorio.")
            .MaximumLength(50).WithMessage("El SKU no puede exceder 50 caracteres.")
            .Matches(@"^[A-Za-z0-9\-_\.]+$").WithMessage("El SKU solo puede contener letras, números, guiones, puntos y guiones bajos.");

        RuleFor(x => x.ShortName)
            .NotEmpty().WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre corto no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(254).WithMessage("La descripción no puede exceder 254 caracteres.");

        RuleFor(x => x.ItemType)
            .IsInEnum().WithMessage("Tipo de ítem inválido.");

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

        RuleFor(x => x.MinStockQty)
            .GreaterThanOrEqualTo(0).WithMessage("El stock mínimo no puede ser negativo.")
            .When(x => x.MinStockQty.HasValue);

        RuleFor(x => x.MaxStockQty)
            .GreaterThanOrEqualTo(0).WithMessage("El stock máximo no puede ser negativo.")
            .When(x => x.MaxStockQty.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MinStockQty.HasValue || !x.MaxStockQty.HasValue || x.MinStockQty <= x.MaxStockQty)
            .WithMessage("El stock mínimo no puede ser mayor que el máximo.")
            .WithName("StockQty");
    }
}
