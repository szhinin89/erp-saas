using FluentValidation;

namespace ERP.Application.Modules.Contabilidad.UseCases.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de cuenta es obligatorio.")
            .MaximumLength(40).WithMessage("El código de cuenta no puede exceder 40 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la cuenta es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre de la cuenta no puede exceder 200 caracteres.");
    }
}
