using FluentValidation;

namespace ERP.Application.Access.UseCases.BootstrapLogin;

public sealed class BootstrapLoginCommandValidator : AbstractValidator<BootstrapLoginCommand>
{
    public BootstrapLoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
