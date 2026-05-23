namespace ERP.Application.Common;

/// <summary>
/// Contexto operacional unificado: SubscriberId + CompanyId + UserId + Role
/// resueltos desde el JWT del request en curso.
///
/// Usar en handlers ERP operativos en lugar de inyectar
/// ICurrentSubscriber + ICurrentCompany + ICurrentUser por separado.
/// Las interfaces individuales siguen disponibles para casos que las necesiten aisladas
/// (auth bootstrap, background jobs, guards especializados).
/// </summary>
public interface IOperationalContext
{
    /// <summary>Id del suscriptor SaaS. <see cref="Guid.Empty"/> si no autenticado o sin claim.</summary>
    Guid SubscriberId { get; }

    /// <summary>Id de la empresa operativa activa. <see cref="Guid.Empty"/> si no hay company_id en el JWT.</summary>
    Guid CompanyId { get; }

    /// <summary>Id del usuario autenticado. <see cref="Guid.Empty"/> si no autenticado.</summary>
    Guid UserId { get; }

    /// <summary>Rol del usuario (Admin, operador platform, User, etc.). Vacío si no autenticado.</summary>
    string Role { get; }

    /// <summary>True cuando <see cref="SubscriberId"/> es válido (no Empty).</summary>
    bool HasSubscriber { get; }

    /// <summary>True cuando <see cref="CompanyId"/> es válido (no Empty).</summary>
    bool HasCompany { get; }

    /// <summary>True cuando el usuario está autenticado con JWT válido.</summary>
    bool IsAuthenticated { get; }

    /// <summary>True cuando el usuario es operador platform global sin suscriptor operativo (bypass RLS controlado).</summary>
    bool IsPlatformAdmin { get; }
}
