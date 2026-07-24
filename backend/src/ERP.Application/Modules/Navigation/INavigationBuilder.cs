using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>
/// Construye el menú de navegación para el usuario actual.
/// Filtra por permisos en backend; el resultado NO contiene metadatos de permiso.
/// </summary>
public interface INavigationBuilder
{
    Task<IReadOnlyList<NavMenuGroupDto>> BuildMenuAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida la entrada de cache del menú para un usuario en una empresa específica.
    /// El menú depende de TenantId + CompanyId + UserId (los permisos efectivos varían por empresa).
    /// </summary>
    void InvalidateCache(Guid tenantId, Guid companyId, Guid userId);
}
