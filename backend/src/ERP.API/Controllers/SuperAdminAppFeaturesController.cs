using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.API.Services;
using ERP.Application.Navigation.DTOs;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Controllers;

[ApiController]
[Modulo("SuperAdmin AppFeatures", "perm:superadmin.AppFeatures.admin", "ðŸ§©", null, null, 984, VisibleEnMenu = false, EsSuperAdmin = true)]
[Route("api/superadmin/AppFeatures")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminAppFeaturesController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly ModuloDiscoveryService _discovery;

    public SuperAdminAppFeaturesController(ErpDbContext db, ModuloDiscoveryService discovery)
    {
        _db = db;
        _discovery = discovery;
    }

    /// <summary>Sincroniza catÃ¡logo desde <c>[Modulo]</c> en controladores/acciones.</summary>
    [HttpPost("sincronizar")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
    {
        var n = await _discovery.SincronizarModulosAsync(ct);
        return this.ApiOk(new { sincronizados = n }, "OK");
    }

    [HttpGet("arbol")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppFeatureTreeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Arbol(CancellationToken ct)
    {
        static string ExtractFunctionalModuleKey(string? ruta, string? permiso, string? nombre)
        {
            var route = (ruta ?? string.Empty).Trim().ToLowerInvariant();
            if (route.StartsWith("/"))
            {
                var first = route.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                    return first;
            }

            var perm = (permiso ?? string.Empty).Trim().ToLowerInvariant();
            if (perm.StartsWith("perm:"))
                perm = perm["perm:".Length..];
            var permModule = perm.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(permModule))
                return permModule;

            var title = (nombre ?? string.Empty).Trim().ToLowerInvariant();
            return title;
        }

        static int FunctionalModuleRank(string moduleKey) => moduleKey switch
        {
            "inventario" => 10,
            "ventas" => 20,
            "compras" => 30,
            "caja" => 40,
            "contabilidad" => 50,
            "gastos" => 60,
            "products" => 70,
            "productos" => 70,
            "access" => 80,
            "security" => 90,
            _ => 500,
        };

        var rows = await _db.AppFeatures
            .AsNoTracking()
            .Where(x => x.IsVisibleInMenu)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Icon,
                x.Path,
                x.Permission,
                x.ParentId,
                x.SortOrder,
            })
            .ToListAsync(ct);

        // No usar Dictionary con clave PadreId == null: en runtime puede lanzarse ArgumentNullException
        // y el ExceptionMiddleware la mapea a HTTP 400.
        List<AppFeatureTreeDto> BuildTree(Guid? parentId)
        {
            return rows
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => FunctionalModuleRank(ExtractFunctionalModuleKey(x.Path, x.Permission, x.Name)))
                .ThenBy(x => ExtractFunctionalModuleKey(x.Path, x.Permission, x.Name))
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new AppFeatureTreeDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Icon = x.Icon,
                    Path = x.Path,
                    Permission = x.Permission,
                    Children = BuildTree(x.Id),
                })
                .ToList();
        }

        var roots = BuildTree(null);
        return this.ApiOk(roots);
    }
}
