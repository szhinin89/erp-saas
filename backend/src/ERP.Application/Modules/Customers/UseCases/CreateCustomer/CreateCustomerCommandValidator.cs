using FluentValidation;

namespace ERP.Application.Modules.Customers.UseCases.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.IdentificationType)
            .NotEmpty().WithMessage("El tipo de identificación es obligatorio.")
            .Must(type => type is not null && (
                string.Equals(type.Trim(), "RUC", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type.Trim(), "CI", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type.Trim(), "CEDULA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type.Trim(), "CÉDULA", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("El tipo de identificación debe ser RUC o CI.");

        RuleFor(x => x.IdentificationNumber)
            .NotEmpty().WithMessage("El número de identificación es obligatorio.")
            .MaximumLength(32).WithMessage("El número de identificación no puede exceder 32 caracteres.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(200).WithMessage("La razón social no puede exceder 200 caracteres.");

        RuleFor(x => x.TradeName)
            .MaximumLength(200).WithMessage("El nombre comercial no puede exceder 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));

        RuleFor(x => x.AddressLine)
            .MaximumLength(400).WithMessage("La dirección no puede exceder 400 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.AddressLine));

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("El teléfono no puede exceder 50 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
