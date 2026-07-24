using FluentValidation;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembership;

public sealed class UpsertCompanyUserMembershipCommandValidator : AbstractValidator<UpsertCompanyUserMembershipCommand>
{
    public UpsertCompanyUserMembershipCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El username del usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El username no puede exceder 50 caracteres.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("El rol es obligatorio.")
            .MaximumLength(50).WithMessage("El rol no puede exceder 50 caracteres.");

        // Único punto de validación no cubierto por ningún otro validador: el formato de
        // DefaultBranchId y LoginMode lo valida el UseCase de CompanyUserPreferences que se
        // invoca internamente (nunca se duplica esa regla aquí).
        RuleForEach(x => x.AuthorizedBranchIds)
            .NotEqual(Guid.Empty)
            .WithMessage("La sucursal a autorizar no puede ser un Guid vacío.");
    }
}
