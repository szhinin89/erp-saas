using FluentValidation;

namespace ERP.Application.Auth.UseCases.ClaimInitialPlatformOperator;

public sealed class ClaimInitialPlatformOperatorCommandValidator : AbstractValidator<ClaimInitialPlatformOperatorCommand>
{
    public ClaimInitialPlatformOperatorCommandValidator()
    {
        RuleFor(x => x.SetupToken)
            .NotEmpty().WithMessage("El token de instalación es obligatorio.")
            .MaximumLength(4096).WithMessage("El token de instalación es demasiado largo.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede exceder 100 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.");
    }
}
