namespace ERP.Application.Common.Interfaces;

/// <summary>Envío del enlace de recuperación (implementación real = SMTP; desarrollo = log).</summary>
public interface IPasswordResetLinkSender
{
    Task SendPasswordResetLinkAsync(string toEmail, string resetLink, CancellationToken ct = default);
}
