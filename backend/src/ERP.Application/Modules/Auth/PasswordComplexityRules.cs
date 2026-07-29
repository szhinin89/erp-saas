using FluentValidation;

namespace ERP.Application.Auth;

/// <summary>
/// Fase H — única política de complejidad de contraseña nueva vigente en todo el sistema
/// (mínimo 8 caracteres, al menos una mayúscula, al menos un dígito). Antes estaba duplicada
/// literalmente en CreateSystemUserCommandValidator y ChangeMyPasswordCommandValidator, y
/// ResetPasswordWithTokenCommandValidator solo exigía longitud mínima (le faltaban las otras
/// dos reglas). Los cuatro validadores que aceptan una contraseña nueva de IdentityUser
/// (Create/Create-System-User, ChangeMyPassword, ResetPasswordWithToken, CompletePasswordReset)
/// aplican esta misma extensión — cambiar la política es cambiar un solo lugar.
/// </summary>
public static class PasswordComplexityRules
{
    public static IRuleBuilderOptions<T, string> ApplyPasswordComplexity<T>(
        this IRuleBuilder<T, string> rule
    ) =>
        rule.MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Matches("[A-Z]")
            .WithMessage("La contraseña debe tener al menos una mayúscula.")
            .Matches("[0-9]")
            .WithMessage("La contraseña debe tener al menos un número.");
}
