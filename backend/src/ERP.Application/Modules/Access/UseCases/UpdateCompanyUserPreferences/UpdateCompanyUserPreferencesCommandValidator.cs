using ERP.Domain.Access.Enums;
using FluentValidation;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;

public sealed class UpdateCompanyUserPreferencesCommandValidator
    : AbstractValidator<UpdateCompanyUserPreferencesCommand>
{
    public UpdateCompanyUserPreferencesCommandValidator()
    {
        RuleFor(x => x.CompanyUserMembershipId)
            .NotEmpty()
            .WithMessage("La membresía es obligatoria.");

        RuleFor(x => x.LoginMode)
            .NotEmpty()
            .WithMessage("El modo de inicio de sesión es obligatorio.")
            .Must(value => Enum.TryParse<CompanyUserLoginMode>(value, out _))
            .WithMessage("El modo de inicio de sesión no es válido.");

        RuleFor(x => x.DefaultBranchId)
            .NotEqual(Guid.Empty)
            .When(x => x.DefaultBranchId.HasValue)
            .WithMessage("La sucursal por defecto no puede ser un Guid vacío.");
    }
}
