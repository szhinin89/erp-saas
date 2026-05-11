using FluentValidation;

namespace ERP.Application.Auth.UseCases.SuperAdminLogin;

public sealed class SuperAdminLoginCommandValidator : AbstractValidator<SuperAdminLoginCommand>
{
    public SuperAdminLoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(200).WithMessage("El email no puede exceder 200 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
