using FluentValidation;

namespace ERP.Application.Modules.Proveedores.UseCases.DisableProveedor;

public sealed class DisableProveedorCommandValidator : AbstractValidator<DisableProveedorCommand>
{
    public DisableProveedorCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del proveedor es obligatorio.");
    }
}
