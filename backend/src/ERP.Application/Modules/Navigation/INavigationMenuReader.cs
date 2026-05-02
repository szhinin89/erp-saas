using ERP.Application.Navigation.DTOs;

namespace ERP.Application.Navigation;

/// <summary>Lee la definición activa del menú lateral desde almacenamiento (p. ej. tablas <c>ui_nav_*</c>).</summary>
public interface INavigationMenuReader
{
    Task<IReadOnlyList<SessionMenuGroupDto>> GetActiveMenuAsync(CancellationToken ct = default);
}
