using FluentValidation;

namespace ERP.Application.Products.UseCases.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del producto es obligatorio.");

        RuleFor(x => x.ShortName)
            .NotEmpty().WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre corto no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(254).WithMessage("La descripción no puede exceder 254 caracteres.");

        RuleFor(x => x.LineId)
            .NotEmpty().WithMessage("La línea es obligatoria.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("La categoría es obligatoria.");

        RuleFor(x => x.SubcategoryId)
            .NotEmpty().WithMessage("La subcategoría es obligatoria.");

        RuleFor(x => x.UomCode)
            .NotEmpty().WithMessage("El código UOM (SRI) es obligatorio.")
            .MaximumLength(10).WithMessage("El código UOM no puede exceder 10 caracteres.");

        RuleFor(x => x.BrandId)
            .NotEmpty().WithMessage("La marca es obligatoria.");

        RuleFor(x => x.ProductTypeId)
            .NotEmpty().WithMessage("El tipo de producto es obligatorio.");

        RuleFor(x => x.TariffId)
            .NotEmpty().WithMessage("El arancel es obligatorio.");

        RuleFor(x => x.MaxItemDiscountPercent)
            .InclusiveBetween(0, 100)
            .WithMessage("El descuento máximo por ítem debe estar entre 0 y 100.");
    }
}
