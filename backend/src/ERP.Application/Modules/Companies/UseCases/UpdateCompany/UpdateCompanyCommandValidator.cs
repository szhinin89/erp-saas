using FluentValidation;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TaxId)
            .Length(13).WithMessage("El RUC debe tener 13 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

        RuleFor(x => x.TradeName).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.TradeName));
        RuleFor(x => x.CorporateEmail).MaximumLength(CompanyEntity.CorporateEmailMaxLen).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CorporateEmail));
        RuleFor(x => x.Website).MaximumLength(CompanyEntity.WebsiteMaxLen).When(x => !string.IsNullOrWhiteSpace(x.Website));
        RuleFor(x => x.CountryCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.BrandingJson).MaximumLength(8000).When(x => !string.IsNullOrWhiteSpace(x.BrandingJson));
    }
}
