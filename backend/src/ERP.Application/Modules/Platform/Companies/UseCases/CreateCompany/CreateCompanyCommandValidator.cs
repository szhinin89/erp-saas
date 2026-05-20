using FluentValidation;

namespace ERP.Application.Modules.Platform.Companies.UseCases.CreateCompany;

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

        RuleFor(x => x.MainAddress)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .MaximumLength(500);

        RuleFor(x => x.TradeName).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.TradeName));
        RuleFor(x => x.Phone).MaximumLength(40).When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).MaximumLength(120).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.CountryCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.LogoUrl).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.LogoUrl));
        RuleFor(x => x.BrandingJson).MaximumLength(8000).When(x => !string.IsNullOrWhiteSpace(x.BrandingJson));
    }
}
