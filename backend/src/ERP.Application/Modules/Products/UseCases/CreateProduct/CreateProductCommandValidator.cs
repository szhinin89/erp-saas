using FluentValidation;

namespace ERP.Application.Products.UseCases.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.SaleCode)
            .NotEmpty().WithMessage("El código de venta es obligatorio.")
            .MaximumLength(50).WithMessage("El código de venta no puede exceder 50 caracteres.");

        RuleFor(x => x.ShortName)
            .NotEmpty().WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre corto no puede exceder 200 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres.");

        RuleFor(x => x.LineId)
            .NotEmpty().WithMessage("La línea es obligatoria.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("La categoría es obligatoria.");

        RuleFor(x => x.SubcategoryId)
            .NotEmpty().WithMessage("La subcategoría es obligatoria.");

        RuleFor(x => x.UnitOfMeasureId)
            .NotEmpty().WithMessage("La unidad de medida es obligatoria.");

        RuleFor(x => x.BrandId)
            .NotEmpty().WithMessage("La marca es obligatoria.");

        RuleFor(x => x.ProductTypeId)
            .NotEmpty().WithMessage("El tipo de producto es obligatorio.");

        RuleFor(x => x.TariffId)
            .NotEmpty().WithMessage("El arancel es obligatorio.");
    }
}
