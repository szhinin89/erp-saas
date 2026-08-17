using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.TaxId)
            .NotEmpty()
            .WithMessage("El RUC / identificador fiscal es obligatorio.")
            .Length(13)
            .WithMessage("El RUC debe tener 13 caracteres.");

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TradeName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));
    }
}
