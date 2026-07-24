using ERP.Application.Auth;
using FluentValidation;

namespace ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;

/// <summary>Misma política de complejidad que CreateSystemUserCommandValidator/ChangeMyPasswordCommandValidator
/// — ver PasswordComplexityRules.</summary>
public sealed class AssignTemporaryPasswordAdminCommandValidator : AbstractValidator<AssignTemporaryPasswordAdminCommand>
{
    public AssignTemporaryPasswordAdminCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.TemporaryPassword).NotEmpty().ApplyPasswordComplexity();
    }
}
