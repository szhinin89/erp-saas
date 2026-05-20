using FluentValidation;

namespace ERP.Application.Access.UseCases.RevokeCompanyUserMembership;

public sealed class RevokeCompanyUserMembershipCommandValidator : AbstractValidator<RevokeCompanyUserMembershipCommand>
{
    public RevokeCompanyUserMembershipCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("El email del usuario es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");
    }
}
