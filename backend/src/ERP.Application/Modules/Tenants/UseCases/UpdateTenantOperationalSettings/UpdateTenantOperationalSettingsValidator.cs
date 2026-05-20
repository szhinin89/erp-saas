using FluentValidation;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;

public sealed class UpdateSubscriberOperationalSettingsValidator
    : AbstractValidator<UpdateSubscriberOperationalSettingsCommand>
{
    private static readonly HashSet<string> AllowedCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "USD", "EUR", "MXN", "COP", "PEN" };

    private static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "es", "en", "qu" };

    public UpdateSubscriberOperationalSettingsValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("La moneda es requerida.")
            .Must(c => AllowedCurrencies.Contains(c))
            .WithMessage("Moneda no soportada. Valores permitidos: USD, EUR, MXN, COP, PEN.");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("El idioma es requerido.")
            .Must(l => AllowedLanguages.Contains(l))
            .WithMessage("Idioma no soportado. Valores permitidos: es, en, qu.");

        RuleFor(x => x.Timezone)
            .NotEmpty().WithMessage("La zona horaria es requerida.")
            .MaximumLength(100).WithMessage("Zona horaria demasiado larga.");

        RuleFor(x => x.InvoicePrefix)
            .MaximumLength(20).WithMessage("El prefijo no puede superar 20 caracteres.")
            .When(x => x.InvoicePrefix is not null);

        RuleFor(x => x.DefaultCreditDays)
            .GreaterThanOrEqualTo(0).WithMessage("Los días de crédito no pueden ser negativos.")
            .LessThanOrEqualTo(365).WithMessage("Los días de crédito no pueden superar 365.");
    }
}
