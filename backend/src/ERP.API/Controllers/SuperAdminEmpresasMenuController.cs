using System;
using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.API.Filters;
using ERP.Application.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Rutas en español para menú por empresa (<c>/api/superadmin/empresas/…</c>).</summary>
[ApiController]
[AppFeature("SuperAdmin Empresas Menú", "perm:superadmin.empresas-menu.admin", "🧩", null, null, 985, IsVisibleInMenu = false, IsSuperAdmin = true)]
[Route("api/superadmin/empresas")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
[DeprecatedApi("/api/platform/subscribers")]
public sealed class SuperAdminEmpresasMenuController : ControllerBase
{
    private readonly ISubscriberMenuAdminService _subscriberMenuAdmin;

    public SuperAdminEmpresasMenuController(ISubscriberMenuAdminService tenantMenuAdmin) =>
        _subscriberMenuAdmin = tenantMenuAdmin;

    public sealed record EmpresaMenuPutBody(string MenuConfigJson);

    [Obsolete("Legacy SuperAdmin route. Use /api/platform/subscribers/{subscriberId}/menu instead.")]
    [HttpGet("{empresaId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(Guid empresaId, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.GetResolvedMenuForTenantAsync(empresaId, ct);
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

    [Obsolete("Legacy SuperAdmin route. Use PUT /api/platform/subscribers/{subscriberId}/menu instead.")]
    [HttpPut("{empresaId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutMenu(Guid empresaId, [FromBody] EmpresaMenuPutBody body, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.UpsertSubscriberCustomMenuAsync(empresaId, body.MenuConfigJson, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    [Obsolete("Legacy SuperAdmin route. Use DELETE /api/platform/subscribers/{subscriberId}/menu instead.")]
    [HttpDelete("{empresaId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMenu(Guid empresaId, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.DeleteSubscriberCustomMenuAsync(empresaId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Restablecido")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
