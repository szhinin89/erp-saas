using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación de ICurrentSubscriber con SubscriberId fijo, usada en background services
/// donde no hay HttpContext ni JWT disponible.
/// </summary>
public sealed class ManualCurrentSubscriber : ICurrentSubscriber
{
    public Guid SubscriberId      { get; }
    public bool IsAuthenticated => true;

    public ManualCurrentSubscriber(Guid subscriberId) => SubscriberId = subscriberId;
}
