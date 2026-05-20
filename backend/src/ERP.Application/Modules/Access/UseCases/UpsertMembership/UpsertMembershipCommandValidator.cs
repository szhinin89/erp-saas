using FluentValidation;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembership;

public sealed class UpsertCompanyUserMembershipCommandValidator : AbstractValidator<UpsertCompanyUserMembershipCommand>
{
    public UpsertCompanyUserMembershipCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("El email del usuario es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("El rol es obligatorio.")
            .MaximumLength(50).WithMessage("El rol no puede exceder 50 caracteres.");
    }
}
