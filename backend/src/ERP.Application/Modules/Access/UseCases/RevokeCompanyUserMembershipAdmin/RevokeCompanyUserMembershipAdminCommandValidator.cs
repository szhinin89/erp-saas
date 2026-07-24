using FluentValidation;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;

/// <summary>
/// Mismas reglas de formato que
/// <see cref="RevokeCompanyUserMembership.RevokeCompanyUserMembershipCommandValidator"/> para
/// UserEmail (sin TenantId — este command nunca lo recibe).
/// </summary>
public sealed class RevokeCompanyUserMembershipAdminCommandValidator : AbstractValidator<RevokeCompanyUserMembershipAdminCommand>
{
    public RevokeCompanyUserMembershipAdminCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El username del usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El username no puede exceder 50 caracteres.");
    }
}
