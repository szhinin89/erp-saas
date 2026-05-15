using FluentValidation;

namespace ERP.Application.Modules.Accounting.UseCases.UpdateAccount;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la cuenta es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la cuenta es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre de la cuenta no puede exceder 200 caracteres.");
    }
}
