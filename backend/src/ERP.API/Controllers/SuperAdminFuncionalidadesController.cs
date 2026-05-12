using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Services;
using ERP.Application.Navigation.DTOs;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Controllers;

[ApiController]
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
        var rows = await _db.Funcionalidades.AsNoTracking()
            .OrderBy(x => x.Orden)
            .ThenBy(x => x.Nombre)
            .Select(x => new { x.Id, x.Nombre, x.Icono, x.Ruta, x.Permiso, x.PadreId })
            .ToListAsync(ct);

        // No usar Dictionary con clave PadreId == null: en runtime puede lanzarse ArgumentNullException
        // y el ExceptionMiddleware la mapea a HTTP 400.
        List<FuncionalidadArbolDto> BuildTree(Guid? parentId)
        {
            return rows
                .Where(x => x.PadreId == parentId)
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
