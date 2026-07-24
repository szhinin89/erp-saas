using FluentValidation;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public sealed class RevokeCompanyUserMembershipCommandValidator : AbstractValidator<RevokeCompanyUserMembershipCommand>
{
    public RevokeCompanyUserMembershipCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El username del usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El username no puede exceder 50 caracteres.");
    }
}
