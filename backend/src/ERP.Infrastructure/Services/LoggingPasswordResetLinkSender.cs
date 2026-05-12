using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>Desarrollo / fallback: registra el enlace. Sustituir por SMTP en producción.</summary>
public sealed class LoggingPasswordResetLinkSender : IPasswordResetLinkSender
{
    private readonly ILogger<LoggingPasswordResetLinkSender> _logger;

    public LoggingPasswordResetLinkSender(ILogger<LoggingPasswordResetLinkSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetLinkAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Password reset link for {Email}: {Link}",
            toEmail,
            resetLink);
        return Task.CompletedTask;
    }
}
