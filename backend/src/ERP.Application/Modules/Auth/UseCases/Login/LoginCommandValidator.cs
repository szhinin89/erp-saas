using FluentValidation;

namespace ERP.Application.Auth.UseCases.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("El username es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El username no puede exceder 50 caracteres.");

        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
