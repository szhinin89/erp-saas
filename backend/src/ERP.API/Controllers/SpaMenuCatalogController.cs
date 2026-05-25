using ERP.API.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Marcadores HTTP solo para que <see cref="ERP.API.Services.ModuloDiscoveryService"/> registre
/// pantallas SPA que no tienen un controlador CRUD dedicado (perfiles, acceso suscriptor, etc.).
/// Las rutas reales las sirve el frontend; aquí no hay lógica de negocio.
/// </summary>
[ApiController]
[Route("api/internal/spa-menu-catalog")]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize] // requiere sesión; operador platform sync usa credenciales de admin
public sealed class SpaMenuCatalogController : ControllerBase
{
    [HttpGet("profiles")]
    [AppFeature("Perfiles (Roles)", "perm:admin.roles.view", "👥", "/admin/roles", null, 86)]
    public IActionResult ProfilesCatalogMarker() => NotFound();

    [HttpGet("subscriber-access")]
    [AppFeature("Acceso usuarios", "perm:admin.users.view", "👤", "/admin/users", null, 87)]
    public IActionResult SubscriberAccessCatalogMarker() => NotFound();

    [HttpGet("empresa")]
    [AppFeature("Datos de Empresa", "perm:settings.company.view", "🏢", "/settings/company", null, 29)]
    public IActionResult EmpresaCatalogMarker() => NotFound();
}
