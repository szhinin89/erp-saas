using FluentValidation;

namespace ERP.Application.Modules.Proveedores.UseCases.EnableProveedor;

public sealed class EnableProveedorCommandValidator : AbstractValidator<EnableProveedorCommand>
{
    public EnableProveedorCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del proveedor es obligatorio.");
    }
}
