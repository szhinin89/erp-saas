using FluentValidation;

namespace ERP.Application.Subscribers.UseCases.UpdatePasswordResetMode;

public sealed class UpdateSubscriberPasswordResetModeCommandValidator : AbstractValidator<UpdateSubscriberPasswordResetModeCommand>
{
    public UpdateSubscriberPasswordResetModeCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.PasswordResetMode)
            .IsInEnum().WithMessage("El modo de recuperación de contraseña no es válido.");
    }
}
