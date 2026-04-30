namespace ERP.Application.Common;

/// <summary>
/// Expone el contexto del tenant activo para el request en curso.
/// La implementación concreta (CurrentTenantService) resuelve el TenantId
/// desde el claim "tenant_id" del JWT en cada request.
///
/// Se inyecta en handlers y repositorios para filtrar datos por tenant
/// sin que el caller deba pasar el TenantId explícitamente.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// Retorna Guid.Empty si el request no está autenticado o el claim no existe.
    /// </summary>
    Guid TenantId { get; }

    bool IsAuthenticated { get; }
}
