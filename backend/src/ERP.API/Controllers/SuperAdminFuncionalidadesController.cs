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
[Modulo("SuperAdmin Funcionalidades", "perm:superadmin.funcionalidades.admin", "🧩", null, null, 984, VisibleEnMenu = false, EsSuperAdmin = true)]
[Route("api/superadmin/funcionalidades")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminFuncionalidadesController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly ModuloDiscoveryService _discovery;

    public SuperAdminFuncionalidadesController(ErpDbContext db, ModuloDiscoveryService discovery)
    {
        _db = db;
        _discovery = discovery;
    }

    /// <summary>Sincroniza catálogo desde <c>[Modulo]</c> en controladores/acciones.</summary>
    [HttpPost("sincronizar")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
    {
        var n = await _discovery.SincronizarModulosAsync(ct);
        return this.ApiOk(new { sincronizados = n }, "OK");
    }

    [HttpGet("arbol")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FuncionalidadArbolDto>>), StatusCodes.Status200OK)]
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

        var rows = await _db.Funcionalidades
            .AsNoTracking()
            .Where(x => x.VisibleEnMenu)
            .Select(x => new
            {
                x.Id,
                x.Nombre,
                x.Icono,
                x.Ruta,
                x.Permiso,
                x.PadreId,
                x.Orden,
            })
            .ToListAsync(ct);

        // No usar Dictionary con clave PadreId == null: en runtime puede lanzarse ArgumentNullException
        // y el ExceptionMiddleware la mapea a HTTP 400.
        List<FuncionalidadArbolDto> BuildTree(Guid? parentId)
        {
            return rows
                .Where(x => x.PadreId == parentId)
                .OrderBy(x => FunctionalModuleRank(ExtractFunctionalModuleKey(x.Ruta, x.Permiso, x.Nombre)))
                .ThenBy(x => ExtractFunctionalModuleKey(x.Ruta, x.Permiso, x.Nombre))
                .ThenBy(x => x.Orden)
                .ThenBy(x => x.Nombre)
                .Select(x => new FuncionalidadArbolDto
                {
                    Id = x.Id,
                    Nombre = x.Nombre,
                    Icono = x.Icono,
                    Ruta = x.Ruta,
                    Permiso = x.Permiso,
                    Hijos = BuildTree(x.Id),
                })
                .ToList();
        }

        var roots = BuildTree(null);
        return this.ApiOk(roots);
    }
}
