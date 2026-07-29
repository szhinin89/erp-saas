using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Desarrollo / fallback: registra el enlace. Sustituir por SMTP en producción.</summary>
public sealed partial class LoggingPasswordResetLinkSender : IPasswordResetLinkSender
{
    private readonly ILogger<LoggingPasswordResetLinkSender> _logger;

    public LoggingPasswordResetLinkSender(ILogger<LoggingPasswordResetLinkSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetLinkAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        LogPasswordResetLink(toEmail, resetLink);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset link for {Email}: {Link}")]
    private partial void LogPasswordResetLink(string email, string link);
}
