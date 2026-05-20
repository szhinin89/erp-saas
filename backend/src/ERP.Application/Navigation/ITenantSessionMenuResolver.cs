using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>Resuelve el menú lateral: menú personalizado del tenant → menú del plan comercial → menú global <c>ui_nav_*</c>.</summary>
public interface ITenantSessionMenuResolver
{
    Task<IReadOnlyList<SessionMenuGroupDto>> ResolveForTenantAsync(Guid subscriberId, CancellationToken ct = default);
}
