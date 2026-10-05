namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Límite de solicitudes de recuperación de contraseña por identidad (email normalizado).
/// La implementación nunca persiste ni registra el email en claro.
/// </summary>
public interface IPasswordResetRequestThrottle
{
    /// <summary>Devuelve <c>false</c> si el email ya agotó su cupo en la ventana vigente.</summary>
    Task<bool> TryAcquireAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default
    );
}
