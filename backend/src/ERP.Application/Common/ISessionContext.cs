namespace ERP.Application.Common;

/// <summary>
/// Contexto de sesión para RLS PostgreSQL (<c>app.subscriber_id</c>, <c>app.company_id</c>).
/// RLS no activado aún — interceptor aplica SET LOCAL cuando hay valores.
/// </summary>
public interface ISessionContext
{
    Guid SubscriberId { get; }
    Guid CompanyId { get; }
    bool HasSubscriberContext { get; }
    bool HasCompanyContext { get; }
    /// <summary>Global super-admin sin suscriptor (bypass RLS controlado).</summary>
    bool IsPlatformAdmin { get; }
}
