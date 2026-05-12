using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Subscriptions;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Controllers;

/// <summary>Rutas en español para menú de planes (<c>/api/superadmin/planes/…</c>).</summary>
[ApiController]
[Route("api/superadmin/planes")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminPlanesMenuController : ControllerBase
{
    private readonly ISaasPlansAdminService _admin;

    public SuperAdminPlanesMenuController(ISaasPlansAdminService admin) => _admin = admin;

    public sealed record PlanMenuPutBody(string? MenuConfigJson);

    [HttpGet("{planId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(Guid planId, CancellationToken ct)
    {
        var r = await _admin.GetPlanMenuJsonAsync(planId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { menuConfigJson = r.Value })
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    [HttpPut("{planId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutMenu(Guid planId, [FromBody] PlanMenuPutBody body, CancellationToken ct)
    {
        var r = await _admin.SetPlanMenuJsonAsync(planId, body.MenuConfigJson, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
