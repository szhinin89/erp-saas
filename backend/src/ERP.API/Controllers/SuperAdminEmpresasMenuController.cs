using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Rutas en español para menú por empresa (<c>/api/superadmin/empresas/…</c>).</summary>
[ApiController]
[Modulo("SuperAdmin Empresas Menú", "perm:superadmin.empresas-menu.admin", "🧩", null, null, 985, VisibleEnMenu = false, EsSuperAdmin = true)]
[Route("api/superadmin/empresas")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminEmpresasMenuController : ControllerBase
{
    private readonly ITenantMenuAdminService _tenantMenuAdmin;

    public SuperAdminEmpresasMenuController(ITenantMenuAdminService tenantMenuAdmin) =>
        _tenantMenuAdmin = tenantMenuAdmin;

    public sealed record EmpresaMenuPutBody(string MenuConfigJson);

    [HttpGet("{empresaId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(Guid empresaId, CancellationToken ct)
    {
        var r = await _tenantMenuAdmin.GetResolvedMenuForTenantAsync(empresaId, ct);
        if (!r.IsSuccess)
            return this.ApiBadRequest(r.Error ?? "Error");
        var v = r.Value!;
        return this.ApiOk(new
        {
            menu = v.Menu,
            hasCustomMenu = v.HasCustomMenu,
            usedPlanMenu = v.UsedPlanMenu,
            usedGlobalFallback = v.UsedGlobalFallback,
        });
    }

    [HttpPut("{empresaId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutMenu(Guid empresaId, [FromBody] EmpresaMenuPutBody body, CancellationToken ct)
    {
        var r = await _tenantMenuAdmin.UpsertTenantCustomMenuAsync(empresaId, body.MenuConfigJson, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    [HttpDelete("{empresaId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMenu(Guid empresaId, CancellationToken ct)
    {
        var r = await _tenantMenuAdmin.DeleteTenantCustomMenuAsync(empresaId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Restablecido")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
