using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.HabilitarProveedor;

public sealed class EnableProveedorCommandValidator : AbstractValidator<EnableProveedorCommand>
{
    public EnableProveedorCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del proveedor es obligatorio.");
    }
}
