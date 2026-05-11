using FluentValidation;

namespace ERP.Application.Modules.Customers.UseCases.DisableCustomer;

public sealed class DisableCustomerCommandValidator : AbstractValidator<DisableCustomerCommand>
{
    public DisableCustomerCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del cliente es obligatorio.");
    }
}
