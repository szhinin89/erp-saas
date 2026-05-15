using FluentValidation;

namespace ERP.Application.Modules.Purchasing.UseCases.HabilitarProveedor;

public sealed class EnableSupplierCommandValidator : AbstractValidator<EnableSupplierCommand>
{
    public EnableSupplierCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del Supplier es obligatorio.");
    }
}
