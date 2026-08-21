using FluentValidation;

namespace ERP.Application.Modules.Communications.UseCases.SendTestEmail;

public sealed class SendTestEmailCommandValidator : AbstractValidator<SendTestEmailCommand>
{
    public SendTestEmailCommandValidator()
    {
        RuleFor(c => c.ToEmail)
            .NotEmpty()
            .WithMessage("El destinatario del correo de prueba es obligatorio.")
            .EmailAddress()
            .WithMessage("El destinatario del correo de prueba no es un correo válido.");
    }
}
