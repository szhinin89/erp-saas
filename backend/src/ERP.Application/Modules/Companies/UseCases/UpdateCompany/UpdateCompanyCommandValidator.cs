using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TaxId)
            .Length(13)
            .WithMessage("El RUC debe tener 13 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

        RuleFor(x => x.TradeName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));
    }
}
