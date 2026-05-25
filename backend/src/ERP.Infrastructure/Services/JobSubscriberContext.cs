namespace ERP.Infrastructure.Services;

/// <summary>
/// Almacena el SubscriberId del job activo usando AsyncLocal, que fluye a través de await.
/// Usado por <see cref="CurrentSubscriberService"/> como fallback cuando no hay HttpContext
/// (jobs de Hangfire, background services).
/// </summary>
public static class JobSubscriberContext
{
    private static readonly AsyncLocal<Guid> _subscriberId = new();

    public static Guid Current
    {
        get => _subscriberId.Value;
        set => _subscriberId.Value = value;
    }
}
