using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Entrega SIMULADA temporal (no envía correo). ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01: nunca
/// registra el enlace, el token ni el email — en ningún entorno, Development incluido. Se reemplaza
/// por el adapter sobre Communications (ADR-039, fase 6).
/// </summary>
public sealed partial class LoggingPasswordResetLinkSender : IPasswordResetLinkSender
{
    private readonly ILogger<LoggingPasswordResetLinkSender> _logger;

    public LoggingPasswordResetLinkSender(ILogger<LoggingPasswordResetLinkSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetLinkAsync(
        string toEmail,
        string resetLink,
        CancellationToken cancellationToken = default
    )
    {
        LogSimulatedDelivery();
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 4104,
        EventName = "PasswordResetDeliverySimulated",
        Level = LogLevel.Warning,
        Message = "Recuperación de contraseña: entrega simulada, sin transporte de correo configurado (el enlace no se envía ni se registra)."
    )]
    private partial void LogSimulatedDelivery();
}
