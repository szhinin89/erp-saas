using FluentValidation;

namespace ERP.Application.Configuration.UseCases.UpsertBillingSettings;

public sealed class UpsertBillingSettingsCommandValidator
    : AbstractValidator<UpsertBillingSettingsCommand>
{
    public UpsertBillingSettingsCommandValidator()
    {
        RuleFor(x => x.IdentificationType)
            .NotEmpty()
            .MaximumLength(5);

        RuleFor(x => x.IdentificationNumber)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.SpecialTaxpayer)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.SpecialTaxpayer));

        RuleFor(x => x.LogoBase64)
            .MaximumLength(10000)
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