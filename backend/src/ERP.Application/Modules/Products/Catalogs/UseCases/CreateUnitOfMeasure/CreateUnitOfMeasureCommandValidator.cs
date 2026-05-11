using FluentValidation;

namespace ERP.Application.Products.Catalogs.UseCases.CreateUnitOfMeasure;

public sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de unidad es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de unidad es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");

        RuleFor(x => x.Symbol)
            .MaximumLength(20).WithMessage("El símbolo no puede exceder 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Symbol));
    }
}
