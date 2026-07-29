using FluentValidation;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;

/// <summary>
/// Deliberadamente mínimo: el formato de LoginMode/DefaultBranchId lo valida
/// UpdateCompanyUserPreferencesCommandValidator (Fase C) cuando este comando delega en él —
/// duplicar esa regla aquí violaría la restricción de no reimplementar validaciones existentes.
/// </summary>
public sealed class UpdateCompanyUserPreferencesAdminCommandValidator
    : AbstractValidator<UpdateCompanyUserPreferencesAdminCommand>
{
    public UpdateCompanyUserPreferencesAdminCommandValidator()
    {
        RuleFor(x => x.CompanyUserId)
            .NotEmpty()
            .WithMessage("El usuario de empresa es obligatorio.");
    }
}
