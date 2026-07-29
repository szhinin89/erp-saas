using FluentValidation;

namespace ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;

/// <summary>
/// Mismas reglas de formato que <see cref="UpsertCompanyUserMembership.UpsertCompanyUserMembershipCommandValidator"/>
/// para UserEmail/Role/AuthorizedBranchIds (sin TenantId — este command nunca lo recibe). No se
/// duplica ninguna regla de negocio: TenantId/CompanyId se resuelven en el handler y el resto de
/// reglas (ProfileId, DefaultBranchId, LoginMode) las sigue validando el UseCase de Fase D al que
/// se delega.
/// </summary>
public sealed class UpsertCompanyUserMembershipAdminCommandValidator
    : AbstractValidator<UpsertCompanyUserMembershipAdminCommand>
{
    public UpsertCompanyUserMembershipAdminCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("El username del usuario es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El username no puede exceder 50 caracteres.");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("El rol es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El rol no puede exceder 50 caracteres.");

        RuleForEach(x => x.AuthorizedBranchIds)
            .NotEqual(Guid.Empty)
            .WithMessage("La sucursal a autorizar no puede ser un Guid vacío.");
    }
}
