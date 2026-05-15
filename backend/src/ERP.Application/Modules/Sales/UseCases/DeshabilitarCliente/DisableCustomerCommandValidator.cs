using FluentValidation;

namespace ERP.Application.Modules.Sales.UseCases.DeshabilitarCliente;

public sealed class DisableCustomerCommandValidator : AbstractValidator<DisableCustomerCommand>
{
    public DisableCustomerCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del cliente es obligatorio.");
    }
}
