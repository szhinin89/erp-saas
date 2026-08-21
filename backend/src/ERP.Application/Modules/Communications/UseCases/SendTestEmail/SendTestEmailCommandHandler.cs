using System.Net.Mail;
using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.SendTestEmail;

public sealed class SendTestEmailCommandHandler
    : IRequestHandler<SendTestEmailCommand, Result<SendTestEmailResultDto>>
{
    private readonly ICommunicationSettingsResolver _settingsResolver;
    private readonly IEmailSender _emailSender;

    public SendTestEmailCommandHandler(
        ICommunicationSettingsResolver settingsResolver,
        IEmailSender emailSender
    )
    {
        _settingsResolver = settingsResolver;
        _emailSender = emailSender;
    }

    public async Task<Result<SendTestEmailResultDto>> Handle(
        SendTestEmailCommand command,
        CancellationToken cancellationToken
    )
    {
        var settings = await _settingsResolver.ResolveEmailAsync(cancellationToken);
        if (!settings.CanSend)
        {
            return Result<SendTestEmailResultDto>.ValidationFailure(
                "La configuración SMTP está incompleta o inactiva — revisa host, puerto, remitente y que el envío esté activo."
            );
        }

        var message = new EmailMessage(
            ToEmail: command.ToEmail,
            ToName: null,
            Subject: "ERP SaaS — correo de prueba de configuración SMTP",
            BodyHtml: "<p>Este es un correo de prueba enviado desde la configuración de Comunicaciones del ERP.</p>"
                + "<p>Si lo recibiste, la configuración SMTP de tu empresa funciona correctamente.</p>",
            BodyText: "Este es un correo de prueba enviado desde la configuración de Comunicaciones del ERP. "
                + "Si lo recibiste, la configuración SMTP de tu empresa funciona correctamente.",
            Attachments: []
        );

        try
        {
            await _emailSender.SendAsync(message, settings, cancellationToken);
        }
        catch (SmtpException ex)
        {
            return Result<SendTestEmailResultDto>.ValidationFailure(
                $"No se pudo enviar el correo de prueba: el servidor SMTP rechazó la conexión o las credenciales ({ex.Message})."
            );
        }
        catch (OperationCanceledException)
        {
            return Result<SendTestEmailResultDto>.ValidationFailure(
                "No se pudo enviar el correo de prueba: tiempo de espera agotado al conectar con el servidor SMTP."
            );
        }

        return Result<SendTestEmailResultDto>.Success(
            new SendTestEmailResultDto(Sent: true, Message: $"Correo de prueba enviado a {command.ToEmail}.")
        );
    }
}
