using FluentValidation;

namespace ERP.Application.Access.UseCases.RevokeMembership;

public sealed class RevokeMembershipCommandValidator : AbstractValidator<RevokeMembershipCommand>
{
    public RevokeMembershipCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("El email del usuario es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");
    }
}
