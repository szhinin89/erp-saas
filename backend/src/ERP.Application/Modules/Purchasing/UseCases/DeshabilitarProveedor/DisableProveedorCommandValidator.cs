using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.DeshabilitarProveedor;

public sealed class DisableSupplierCommandValidator : AbstractValidator<DisableSupplierCommand>
{
    public DisableSupplierCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del Supplier es obligatorio.");
    }
}
