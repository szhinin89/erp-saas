namespace ERP.Application.Common;

public interface IOperationalContext
{
    /// <summary>Id del tenant. <see cref="Guid.Empty"/> si no autenticado o sin claim.</summary>
    Guid TenantId { get; }

    /// <summary>Id de la empresa operativa activa. <see cref="Guid.Empty"/> si no hay company_id en el JWT.</summary>
    Guid CompanyId { get; }

    /// <summary>Id del usuario autenticado. <see cref="Guid.Empty"/> si no autenticado.</summary>
    Guid UserId { get; }

    /// <summary>Rol del usuario. Vacío si no autenticado.</summary>
    string Role { get; }

    /// <summary>True cuando <see cref="TenantId"/> es válido (no Empty).</summary>
    bool HasTenant { get; }

    /// <summary>True cuando <see cref="CompanyId"/> es válido (no Empty).</summary>
    bool HasCompany { get; }

    /// <summary>True cuando el usuario está autenticado con JWT válido.</summary>
    bool IsAuthenticated { get; }

}
