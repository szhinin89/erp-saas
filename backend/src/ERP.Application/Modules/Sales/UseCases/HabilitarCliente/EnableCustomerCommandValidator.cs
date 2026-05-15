using FluentValidation;

namespace ERP.Application.Modules.Sales.UseCases.HabilitarCliente;

public sealed class EnableCustomerCommandValidator : AbstractValidator<EnableCustomerCommand>
{
    public EnableCustomerCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del cliente es obligatorio.");
    }
}
