using FluentValidation;

namespace ERP.Application.Items.UseCases.CreateItem;

public sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
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

        // NOTA: ItemTypeId es un catálogo tenant-editable (item_types) — la existencia +
        // estado activo del Id se verifica en el Handler (IItemTypeRepository), no
        // aquí, porque FluentValidation síncrono no tiene acceso a base de datos en este
        // proyecto (no existe patrón de validador async establecido).
        RuleFor(x => x.ItemTypeId).NotEmpty().WithMessage("El tipo de ítem es obligatorio.");

        RuleFor(x => x.DefaultUomCode)
            .NotEmpty()
            .WithMessage("La unidad de medida base es obligatoria.")
            .MaximumLength(10)
            .WithMessage("El código UOM no puede exceder 10 caracteres.");

        RuleFor(x => x.CategoryNodeId).NotEmpty().WithMessage("La categoría es obligatoria.");

        RuleFor(x => x.BrandId).NotEmpty().WithMessage("La marca es obligatoria.");

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

        RuleFor(x => x.Barcodes)
            .NotEmpty()
            .WithMessage("Debe registrar al menos un código de barras.");

        RuleForEach(x => x.Barcodes)
            .ChildRules(b =>
            {
                b.RuleFor(x => x.Code)
                    .NotEmpty()
                    .WithMessage("El código de barras es obligatorio.")
                    .MaximumLength(100)
                    .WithMessage("El código de barras no puede exceder 100 caracteres.");

                // NOTA: la existencia + estado activo del código en el catálogo global
                // barcode_types se verifica en el Handler (IItemCatalogRepository).
                b.RuleFor(x => x.BarcodeType)
                    .NotEmpty()
                    .WithMessage("El tipo de código de barras es obligatorio.");
            });

        RuleFor(x => x.Barcodes)
            .Must(list => list.Count(b => b.IsPrimary) == 1)
            .WithMessage("Debe marcar exactamente un código de barras como principal.")
            .When(x => x.Barcodes is { Count: > 0 });

        RuleFor(x => x.Barcodes)
            .Must(list =>
                list.Select(b => b.Code.Trim().ToUpperInvariant()).Distinct().Count() == list.Count
            )
            .WithMessage("Los códigos de barras no pueden repetirse.")
            .When(x => x.Barcodes is { Count: > 0 });

        RuleForEach(x => x.SupplierCodes)
            .ChildRules(s =>
            {
                s.RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
                s.RuleFor(x => x.Code)
                    .NotEmpty()
                    .WithMessage("El código de proveedor es obligatorio.")
                    .MaximumLength(100)
                    .WithMessage("El código de proveedor no puede exceder 100 caracteres.");
                s.RuleFor(x => x.PackagingLevelId)
                    .Must(id => id is null || id.Value != Guid.Empty)
                    .WithMessage("El nivel de empaque no es válido.");
            });

        RuleFor(x => x.SupplierCodes)
            .Must(list => list == null || list.Count(s => s.IsPrimary) <= 1)
            .WithMessage("Solo un código de proveedor puede ser principal.");

        RuleFor(x => x.BaseSalePrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El precio base no puede ser negativo.")
            .When(x => x.BaseSalePrice.HasValue);

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
