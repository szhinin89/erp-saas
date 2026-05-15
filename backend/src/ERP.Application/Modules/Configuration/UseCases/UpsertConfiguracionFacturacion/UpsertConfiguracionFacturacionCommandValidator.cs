using FluentValidation;

namespace ERP.Application.Configuration.UseCases.UpsertBillingSettings;

public sealed class UpsertBillingSettingsCommandValidator
    : AbstractValidator<UpsertBillingSettingsCommand>
{
    public UpsertBillingSettingsCommandValidator()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.TradeName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Ruc)
            .NotEmpty()
            .Length(13);

        RuleFor(x => x.MainAddress)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(25);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.SpecialTaxpayer)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.SpecialTaxpayer));

        RuleFor(x => x.LogoBase64)
            .MaximumLength(100000)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoBase64));

        RuleFor(x => x.FooterText)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.FooterText));

        RuleFor(x => x.ReceiptWidth)
            .InclusiveBetween(58, 80)
            .Must(x => x == 58 || x == 80)
            .WithMessage("El ancho de tirilla debe ser 58 o 80 mm.");
    }
}
