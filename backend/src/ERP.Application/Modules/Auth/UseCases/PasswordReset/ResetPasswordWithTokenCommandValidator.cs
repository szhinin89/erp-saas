using ERP.Application.Auth;
using FluentValidation;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ResetPasswordWithTokenCommandValidator : AbstractValidator<ResetPasswordWithTokenCommand>
{
    public ResetPasswordWithTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token es obligatorio.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .ApplyPasswordComplexity();
    }
}
