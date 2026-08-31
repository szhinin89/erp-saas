using FluentValidation;

namespace ERP.Application.Auth.UseCases.Reauthenticate;

public sealed class ReauthenticateCommandValidator : AbstractValidator<ReauthenticateCommand>
{
    public ReauthenticateCommandValidator()
    {
        RuleFor(x => x.RawRefreshToken)
            .NotEmpty()
            .WithMessage("Se requiere una sesión activa para reautenticar.");

        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
