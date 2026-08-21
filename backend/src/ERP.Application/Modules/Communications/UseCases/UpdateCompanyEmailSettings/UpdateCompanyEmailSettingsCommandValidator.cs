using FluentValidation;

namespace ERP.Application.Modules.Communications.UseCases.UpdateCompanyEmailSettings;

public sealed class UpdateCompanyEmailSettingsCommandValidator
    : AbstractValidator<UpdateCompanyEmailSettingsCommand>
{
    public UpdateCompanyEmailSettingsCommandValidator()
    {
        When(
            c => c.Enabled,
            () =>
            {
                RuleFor(c => c.SmtpHost)
                    .NotEmpty()
                    .WithMessage("El host SMTP es obligatorio cuando el envío está activo.");
                RuleFor(c => c.SmtpUsername)
                    .NotEmpty()
                    .WithMessage("El usuario SMTP es obligatorio cuando el envío está activo.");
                RuleFor(c => c.SenderEmail)
                    .NotEmpty()
                    .WithMessage("El correo remitente es obligatorio cuando el envío está activo.");
            }
        );

        When(
            c => c.SmtpPort.HasValue,
            () =>
                RuleFor(c => c.SmtpPort!.Value)
                    .InclusiveBetween(1, 65535)
                    .WithMessage("El puerto SMTP debe estar entre 1 y 65535.")
        );

        When(
            c => !string.IsNullOrWhiteSpace(c.SenderEmail),
            () =>
                RuleFor(c => c.SenderEmail)
                    .EmailAddress()
                    .WithMessage("El correo remitente no es válido.")
        );

        When(
            c => !string.IsNullOrWhiteSpace(c.ReplyToEmail),
            () =>
                RuleFor(c => c.ReplyToEmail)
                    .EmailAddress()
                    .WithMessage("El correo de respuesta (Reply-To) no es válido.")
        );

        When(
            c => c.MaxRetries.HasValue,
            () =>
                RuleFor(c => c.MaxRetries!.Value)
                    .InclusiveBetween(0, 20)
                    .WithMessage("Los reintentos máximos deben estar entre 0 y 20.")
        );
    }
}
