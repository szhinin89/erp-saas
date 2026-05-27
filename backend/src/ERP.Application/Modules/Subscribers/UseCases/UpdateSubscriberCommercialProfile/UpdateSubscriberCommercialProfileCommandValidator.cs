using FluentValidation;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberCommercialProfile;

public sealed class UpdateSubscriberCommercialProfileCommandValidator : AbstractValidator<UpdateSubscriberCommercialProfileCommand>
{
    private static readonly HashSet<string> AllowedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "es", "en", "qu" };

    public UpdateSubscriberCommercialProfileCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la empresa es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("El slug es obligatorio.")
            .MaximumLength(100).WithMessage("El slug no puede exceder 100 caracteres.");

        RuleFor(x => x.PreferredLanguage)
            .NotEmpty().WithMessage("El idioma es requerido.")
            .Must(l => AllowedLanguages.Contains(l))
            .WithMessage("Idioma no soportado. Valores permitidos: es, en, qu.");
    }
}
