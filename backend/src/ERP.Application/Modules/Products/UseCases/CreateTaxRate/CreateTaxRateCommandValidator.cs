using FluentValidation;

namespace ERP.Application.Products.UseCases.CreateTaxRate;

public sealed class CreateTaxRateCommandValidator : AbstractValidator<CreateTaxRateCommand>
{
    public CreateTaxRateCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de tarifa es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de tarifa es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de tarifa no es válido.");

        RuleFor(x => x.Percentage)
            .InclusiveBetween(0, 100).WithMessage("El porcentaje debe estar entre 0 y 100.");
    }
}
