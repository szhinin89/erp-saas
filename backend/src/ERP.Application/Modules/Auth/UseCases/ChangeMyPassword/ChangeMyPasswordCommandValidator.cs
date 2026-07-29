using FluentValidation;

namespace ERP.Application.Auth.UseCases.ChangeMyPassword;

/// <summary>Reglas de complejidad: ver PasswordComplexityRules — única política vigente de qué
/// hace válida una contraseña nueva de IdentityUser en todo el sistema.</summary>
public sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().ApplyPasswordComplexity();
    }
}
