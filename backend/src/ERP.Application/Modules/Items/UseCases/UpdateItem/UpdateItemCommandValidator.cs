using FluentValidation;

namespace ERP.Application.Items.UseCases.UpdateItem;

public sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El Id del ítem es obligatorio.");

        RuleFor(x => x.SKU)
            .NotEmpty()
            .WithMessage("El SKU es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El SKU no puede exceder 50 caracteres.")
            .Matches(@"^[A-Za-z0-9\-_\.]+$")
            .WithMessage(
                "El SKU solo puede contener letras, números, guiones, puntos y guiones bajos."
            );

        RuleFor(x => x.ShortName)
            .NotEmpty()
            .WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El nombre corto no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("La descripción es obligatoria.")
            .MaximumLength(254)
            .WithMessage("La descripción no puede exceder 254 caracteres.");

        RuleFor(x => x.Observations)
            .MaximumLength(500)
            .WithMessage("Las observaciones no pueden exceder 500 caracteres.")
            .When(x => x.Observations != null);

        RuleFor(x => x.DefaultUomCode)
            .NotEmpty()
            .WithMessage("La unidad de medida base es obligatoria.")
            .MaximumLength(10)
            .WithMessage("El código UOM no puede exceder 10 caracteres.");

        RuleFor(x => x.SaleVatCode)
            .MaximumLength(10)
            .WithMessage("El código de IVA de venta no puede exceder 10 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.SaleVatCode));

        RuleFor(x => x.PurchaseVatCode)
            .MaximumLength(10)
            .WithMessage("El código de IVA de compra no puede exceder 10 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.PurchaseVatCode));

        RuleFor(x => x.ExciseTaxCode)
            .MaximumLength(10)
            .WithMessage("El código de ICE no puede exceder 10 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ExciseTaxCode));

        // BaseSalePrice es SSOT del Item (ADR-021) — obligatorio en Update para que la UI
        // nunca pueda omitirlo y provocar un overwrite silencioso a null (ver UpdateItemCommandHandler).
        RuleFor(x => x.BaseSalePrice)
            .NotNull()
            .WithMessage("El precio base es obligatorio.")
            .GreaterThanOrEqualTo(0)
            .WithMessage("El precio base no puede ser negativo.");

        RuleFor(x => x.MaxDiscountPercent)
            .InclusiveBetween(0, 100)
            .WithMessage("El descuento máximo debe estar entre 0 y 100.")
            .When(x => x.MaxDiscountPercent.HasValue);

        RuleFor(x => x.MinStockQty)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El stock mínimo no puede ser negativo.")
            .When(x => x.MinStockQty.HasValue);

        RuleFor(x => x.MaxStockQty)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El stock máximo no puede ser negativo.")
            .When(x => x.MaxStockQty.HasValue);

        RuleFor(x => x)
            .Must(x =>
                !x.MinStockQty.HasValue || !x.MaxStockQty.HasValue || x.MinStockQty <= x.MaxStockQty
            )
            .WithMessage("El stock mínimo no puede ser mayor que el máximo.")
            .WithName("StockQty");
    }
}
