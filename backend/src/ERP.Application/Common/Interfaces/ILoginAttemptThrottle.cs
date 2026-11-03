namespace ERP.Application.Common.Interfaces;

/// <summary>
/// SEC-A01 — admisión de <c>login</c>/<c>global-login</c> antes de ejecutar el handler: reserva
/// atómica de cupos (cuenta+IP, IP, cuenta) y control de concurrencia por instancia.
/// </summary>
public interface ILoginAttemptThrottle
{
    /// <param name="identifier">Usuario tal como llega en el command (se normaliza y se hashea).</param>
    /// <param name="clientIp">IP del cliente resuelta por el host; nula si no se conoce.</param>
    /// <param name="cancellationToken">Cancelación de la solicitud.</param>
    Task<ILoginAttempt> BeginAsync(
        string identifier,
        string? clientIp,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Intento admitido o rechazado. <see cref="IDisposable.Dispose"/> libera el permiso de concurrencia;
/// la reserva de cupos solo se devuelve con <see cref="MarkSucceededAsync"/> (un fallo, un error o una
/// cancelación la conservan hasta que vence la ventana).
/// </summary>
public interface ILoginAttempt : IDisposable
{
    LoginAdmissionStatus Status { get; }

    Task MarkSucceededAsync();
}

public enum LoginAdmissionStatus
{
    Admitted,
    RateLimited,
    Unavailable,
}
