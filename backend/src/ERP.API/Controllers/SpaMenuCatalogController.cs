using ERP.API.Attributes;
using ERP.Domain.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Marcadores HTTP solo para que <c>AppFeatureDiscoveryService</c> registre
/// pantallas SPA que no tienen un controlador CRUD dedicado (perfiles, acceso suscriptor, etc.).
/// Las rutas reales las sirve el frontend; aquí no hay lógica de negocio.
/// </summary>
[ApiController]
[Route("api/v1/internal/spa-menu-catalog")]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize] // requiere sesión; operador platform sync usa credenciales de admin
public sealed class SpaMenuCatalogController : ControllerBase
{
    [HttpGet("profiles")]
    [AppFeature(
        "Perfiles (Roles)",
        $"perm:{AccessPermissions.ProfilesView}",
        "👥",
        "/admin/roles",
        null,
        86
    )]
    public IActionResult ProfilesCatalogMarker() => NotFound();

    [HttpGet("tenant-access")]
    [AppFeature(
        "Acceso usuarios",
        $"perm:{AccessPermissions.MembershipsView}",
        "👤",
        "/access/users",
        null,
        87
    )]
    public IActionResult TenantAccessCatalogMarker() => NotFound();

    [HttpGet("empresa")]
    [AppFeature(
        "Datos de Empresa",
        $"perm:{SettingsPermissions.CompanyView}",
        "🏢",
        "/settings/company",
        null,
        29
    )]
    public IActionResult EmpresaCatalogMarker() => NotFound();
}
