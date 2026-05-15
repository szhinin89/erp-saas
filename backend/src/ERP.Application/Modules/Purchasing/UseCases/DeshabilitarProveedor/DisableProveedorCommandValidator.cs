using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.DeshabilitarProveedor;

public sealed class DisableProveedorCommandValidator : AbstractValidator<DisableProveedorCommand>
{
    public DisableProveedorCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del Supplier es obligatorio.");
    }
}
