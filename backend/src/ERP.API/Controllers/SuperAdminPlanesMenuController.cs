using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Rutas en español para menú de planes (<c>/api/superadmin/planes/…</c>).</summary>
[ApiController]
[AppFeature("SuperAdmin Planes Menú", "perm:superadmin.planes-menu.admin", "🧩", null, null, 981, IsVisibleInMenu = false, IsSuperAdmin = true)]
[Route("api/superadmin/planes")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminPlanesMenuController : ControllerBase
{
    private readonly ISaasPlansAdminService _admin;

    public SuperAdminPlanesMenuController(ISaasPlansAdminService admin) => _admin = admin;

    public sealed record PlanMenuPutBody(string? MenuConfigJson, string? MenuSidebarLayout);

    [HttpGet("{planId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(Guid planId, CancellationToken ct)
    {
        var r = await _admin.GetPlanMenuAsync(planId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { menuConfigJson = r.Value.MenuConfigJson, menuSidebarLayout = r.Value.MenuSidebarLayout })
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    [HttpPut("{planId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutMenu(Guid planId, [FromBody] PlanMenuPutBody body, CancellationToken ct)
    {
        var r = await _admin.SetPlanMenuJsonAsync(planId, body.MenuConfigJson, body.MenuSidebarLayout, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Misma semántica que <c>POST /api/superadmin/saas-plans/{target}/copy-from/{source}</c>.</summary>
    [HttpPost("{targetPlanId:guid}/copy-from/{sourcePlanId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CopyPlanFrom(
        Guid targetPlanId,
        Guid sourcePlanId,
        [FromBody] CopyPlanFromRequest? body,
        CancellationToken ct)
    {
        var b = body ?? new CopyPlanFromRequest();
        var r = await _admin.CopyPlanFromAsync(targetPlanId, sourcePlanId, b.CopyMenu, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Copiado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
