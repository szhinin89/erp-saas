namespace ERP.Application.Common;

/// <summary>
/// Expone el contexto del tenant activo para el request en curso.
/// La implementación concreta (CurrentSubscriberService) resuelve el SubscriberId
/// desde el claim "subscriber_id" del JWT en cada request.
///
/// Se inyecta en handlers y repositorios para filtrar datos por tenant
/// sin que el caller deba pasar el SubscriberId explícitamente.
/// </summary>
public interface ICurrentSubscriber
{
    /// <summary>
    /// Retorna Guid.Empty si el request no está autenticado o el claim no existe.
    /// </summary>
    Guid SubscriberId { get; }

    bool IsAuthenticated { get; }
}
