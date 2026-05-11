using FluentValidation;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductSubcategory;

public sealed class CreateProductSubcategoryCommandValidator : AbstractValidator<CreateProductSubcategoryCommand>
{
    public CreateProductSubcategoryCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de subcategoría es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de subcategoría es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("La categoría es obligatoria.");
    }
}
