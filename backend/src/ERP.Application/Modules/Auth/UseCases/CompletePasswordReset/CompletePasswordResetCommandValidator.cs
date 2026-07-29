using FluentValidation;

namespace ERP.Application.Auth.UseCases.CompletePasswordReset;

public sealed class CompletePasswordResetCommandValidator : AbstractValidator<CompletePasswordResetCommand>
{
    public CompletePasswordResetCommandValidator()
    {
        RuleFor(x => x.PasswordResetToken)
            .NotEmpty().WithMessage("El token es obligatorio.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .ApplyPasswordComplexity();
    }
}
