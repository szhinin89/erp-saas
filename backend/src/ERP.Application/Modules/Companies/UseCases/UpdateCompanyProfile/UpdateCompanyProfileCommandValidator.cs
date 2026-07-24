using FluentValidation;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;

public sealed class UpdateCompanyProfileCommandValidator : AbstractValidator<UpdateCompanyProfileCommand>
{
    public UpdateCompanyProfileCommandValidator()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TradeName).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.TradeName));

        RuleFor(x => x.TaxIdentificationNumber)
            .Length(13).WithMessage("El RUC debe tener 13 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxIdentificationNumber));

        RuleFor(x => x.CorporateEmail)
            .MaximumLength(CompanyEntity.CorporateEmailMaxLen)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.CorporateEmail));

        RuleFor(x => x.Website)
            .MaximumLength(CompanyEntity.WebsiteMaxLen)
            .Must(IsValidUrl).WithMessage("La URL del sitio web no es válida.")
            .When(x => !string.IsNullOrWhiteSpace(x.Website));

        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(64);

        RuleFor(x => x.Phone).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.LegalRepName).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.LegalRepName));
        RuleFor(x => x.LegalRepPosition).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.LegalRepPosition));
        RuleFor(x => x.LegalRepIdNumber).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.LegalRepIdNumber));
        RuleFor(x => x.LegalRepEmail)
            .MaximumLength(CompanyEntity.CorporateEmailMaxLen)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.LegalRepEmail));
        RuleFor(x => x.LegalRepPhone).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.LegalRepPhone));
    }

    private static bool IsValidUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) || Uri.TryCreate(value, UriKind.Absolute, out _);
}
