using FluentValidation;

namespace ERP.Application.Products.UseCases.CreateProductLine;

public sealed class CreateProductLineCommandValidator : AbstractValidator<CreateProductLineCommand>
{
    public CreateProductLineCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de línea es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de línea es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");
    }
}
