namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Frontera de Auth para entregar el enlace de recuperación. Auth es dueño del token (emisión,
/// hash, expiración, single-use); la implementación solo entrega. Hoy la única implementación es
/// una entrega simulada que no envía correo ni registra el enlace; la entrega real será un adapter
/// sobre Communications (ADR-039, fase 6).
/// </summary>
public interface IPasswordResetLinkSender
{
    Task SendPasswordResetLinkAsync(
        string toEmail,
        string resetLink,
        CancellationToken cancellationToken = default
    );
}
