using FluentValidation;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberSaasProfile;

public sealed class UpdateSubscriberSaasProfileCommandValidator : AbstractValidator<UpdateSubscriberSaasProfileCommand>
{
    public UpdateSubscriberSaasProfileCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la empresa es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("El slug es obligatorio.")
            .MaximumLength(100).WithMessage("El slug no puede exceder 100 caracteres.");

        RuleFor(x => x.Ruc)
            .MaximumLength(15).WithMessage("El RUC no puede exceder 15 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ruc));

        RuleFor(x => x.ShortName)
            .MaximumLength(100).WithMessage("El nombre corto no puede exceder 100 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ShortName));

        RuleFor(x => x.TradeName)
            .MaximumLength(120).WithMessage("El nombre comercial no puede exceder 120 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));

        RuleFor(x => x.Dinardap)
            .MaximumLength(20).WithMessage("Dinardap no puede exceder 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Dinardap));

        RuleFor(x => x.LogoUrl)
            .MaximumLength(500).WithMessage("La URL del logo no puede exceder 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl));
    }
}
