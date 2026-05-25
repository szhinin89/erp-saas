using FluentValidation;

namespace ERP.Application.Access.UseCases.RegisterSubscriberWithAdmin;

public sealed class RegisterSubscriberWithAdminCommandValidator : AbstractValidator<RegisterSubscriberWithAdminCommand>
{
    public RegisterSubscriberWithAdminCommandValidator()
    {
        RuleFor(x => x.SubscriberName)
            .NotEmpty().WithMessage("El nombre de la empresa es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.SubscriberSlug)
            .NotEmpty().WithMessage("El slug es obligatorio.")
            .MaximumLength(100).WithMessage("El slug no puede exceder 100 caracteres.");

        RuleFor(x => x.AdminFirstName)
            .NotEmpty().WithMessage("El nombre del administrador es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.AdminLastName)
            .NotEmpty().WithMessage("El apellido del administrador es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede exceder 100 caracteres.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("El email del administrador es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage("La contraseña del administrador es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");

        RuleFor(x => x.PasswordResetMode)
            .IsInEnum().WithMessage("El modo de recuperación de contraseña no es válido.");

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
