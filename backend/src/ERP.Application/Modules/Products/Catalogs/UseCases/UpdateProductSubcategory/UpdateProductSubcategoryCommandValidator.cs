using FluentValidation;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductSubcategory;

public sealed class UpdateProductSubcategoryCommandValidator : AbstractValidator<UpdateProductSubcategoryCommand>
{
    public UpdateProductSubcategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de subcategoría es obligatorio.");

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
