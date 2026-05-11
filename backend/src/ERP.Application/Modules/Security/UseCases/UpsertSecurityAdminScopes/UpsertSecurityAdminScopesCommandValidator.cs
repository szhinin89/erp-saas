using FluentValidation;
using ERP.Domain.Security.Entities;

namespace ERP.Application.Security.UseCases.UpsertSecurityAdminScopes;

public sealed class UpsertSecurityAdminScopesCommandValidator : AbstractValidator<UpsertSecurityAdminScopesCommand>
{
    private static readonly int[] ValidScopeInts =
    [
        (int)SecurityAdminScope.ManageRoles,
        (int)SecurityAdminScope.ManageModules,
        (int)SecurityAdminScope.ManageScreens,
        (int)SecurityAdminScope.ManageProcesses,
    ];

    public UpsertSecurityAdminScopesCommandValidator()
    {
        RuleFor(x => x.SubjectType)
            .NotEmpty().WithMessage("SubjectType es obligatorio.")
            .MaximumLength(20).WithMessage("SubjectType no puede exceder 20 caracteres.")
            .Must(t => t.Trim() is "Role" or "User")
            .WithMessage("SubjectType debe ser Role o User.");

        RuleFor(x => x.SubjectKey)
            .NotEmpty().WithMessage("SubjectKey es obligatorio.")
            .MaximumLength(100).WithMessage("SubjectKey no puede exceder 100 caracteres.");

        RuleFor(x => x.AllowedScopes)
            .NotNull().WithMessage("AllowedScopes es obligatorio.");

        RuleForEach(x => x.AllowedScopes)
            .Must(scope => ValidScopeInts.Contains(scope))
            .WithMessage("Cada ámbito (scope) debe ser un valor válido del catálogo de seguridad.");
    }
}
