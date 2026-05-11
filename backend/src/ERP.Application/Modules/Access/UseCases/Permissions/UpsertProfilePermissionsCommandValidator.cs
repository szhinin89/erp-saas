using FluentValidation;

namespace ERP.Application.Access.UseCases.Permissions;

public sealed class UpsertProfilePermissionsCommandValidator : AbstractValidator<UpsertProfilePermissionsCommand>
{
    public UpsertProfilePermissionsCommandValidator()
    {
        RuleFor(x => x.ProfileId)
            .NotEmpty().WithMessage("El perfil es obligatorio.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("La lista de permisos es obligatoria.")
            .NotEmpty().WithMessage("Debe enviar al menos un permiso.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PermissionKey)
                .NotEmpty().WithMessage("La clave de permiso es obligatoria.")
                .MaximumLength(180).WithMessage("La clave de permiso no puede exceder 180 caracteres.");
        });
    }
}
