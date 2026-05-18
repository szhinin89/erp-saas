using ERP.API.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Marcadores HTTP solo para que <see cref="ERP.API.Services.ModuloDiscoveryService"/> registre
/// pantallas SPA que no tienen un controlador CRUD dedicado (perfiles, acceso tenant, etc.).
/// Las rutas reales las sirve el frontend; aquí no hay lógica de negocio.
/// </summary>
[ApiController]
[Route("api/internal/spa-menu-catalog")]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize] // requiere sesión; SuperAdmin sync usa credenciales de admin
public sealed class SpaMenuCatalogController : ControllerBase
{
    [HttpGet("profiles")]
    [AppFeature("Perfiles", "perm:access.profiles.view", "👤", "/profiles", null, 86)]
    public IActionResult ProfilesCatalogMarker() => NotFound();

    [HttpGet("tenant-access")]
    [AppFeature("Acceso usuarios", "perm:access.memberships.view", "🧑‍🤝‍🧑", "/access", null, 87)]
    public IActionResult TenantAccessCatalogMarker() => NotFound();

    // ── Grupo "Configuración" ─────────────────────────────────
    // Carpeta padre en el catálogo para los 3 formularios de configuración de empresa.
    // path = null → aparece como carpeta en la biblioteca del plan builder, no como ruta.
    [HttpGet("configuracion-group")]
    [AppFeature("Configuración", "perm:settings.group", "⚙️", null, null, 28)]
    public IActionResult ConfiguracionGroupMarker() => NotFound();

    [HttpGet("empresa")]
    [AppFeature("Datos de Empresa", "perm:settings.company.view", "🏢", "/settings/company", "perm:settings.group", 29)]
    public IActionResult EmpresaCatalogMarker() => NotFound();
}
