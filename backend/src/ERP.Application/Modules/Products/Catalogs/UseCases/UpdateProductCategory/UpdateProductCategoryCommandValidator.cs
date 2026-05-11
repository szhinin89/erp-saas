using FluentValidation;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductCategory;

public sealed class UpdateProductCategoryCommandValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de categoría es obligatorio.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de categoría es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de categoría es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");

        RuleFor(x => x.LineId)
            .NotEmpty().WithMessage("La línea de producto es obligatoria.");
    }
}
