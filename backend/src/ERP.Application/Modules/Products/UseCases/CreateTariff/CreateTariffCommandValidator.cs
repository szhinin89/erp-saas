using FluentValidation;

namespace ERP.Application.Products.UseCases.CreateTariff;

public sealed class CreateTariffCommandValidator : AbstractValidator<CreateTariffCommand>
{
    public CreateTariffCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de arancel es obligatorio.")
            .MaximumLength(50).WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción del arancel es obligatoria.")
            .MaximumLength(254).WithMessage("La descripción no puede exceder 254 caracteres.");
    }
}
