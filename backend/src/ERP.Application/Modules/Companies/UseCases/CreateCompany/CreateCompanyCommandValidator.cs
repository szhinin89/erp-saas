using FluentValidation;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("El RUC / identificador fiscal es obligatorio.")
            .Length(13).WithMessage("El RUC debe tener 13 caracteres.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TradeName).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.TradeName));
        RuleFor(x => x.CorporateEmail).MaximumLength(CompanyEntity.CorporateEmailMaxLen).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CorporateEmail));
        RuleFor(x => x.Website).MaximumLength(CompanyEntity.WebsiteMaxLen).When(x => !string.IsNullOrWhiteSpace(x.Website));
        RuleFor(x => x.CountryCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.BrandingJson).MaximumLength(8000).When(x => !string.IsNullOrWhiteSpace(x.BrandingJson));
    }
}
