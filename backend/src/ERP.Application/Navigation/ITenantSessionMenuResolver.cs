using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>Resuelve el menú lateral: menú personalizado del subscriber → menú del plan comercial → menú global <c>ui_nav_*</c>.</summary>
public interface ISubscriberSessionMenuResolver
{
    Task<IReadOnlyList<SessionMenuGroupDto>> ResolveForSubscriberAsync(Guid subscriberId, CancellationToken ct = default);
}
