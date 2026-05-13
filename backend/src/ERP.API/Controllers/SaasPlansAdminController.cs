using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>CRUD de planes SaaS y catálogo de features (solo SuperAdmin).</summary>
[ApiController]
[Route("api/superadmin/saas-plans")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SaasPlansAdminController : ControllerBase
{
    private readonly ISaasPlansAdminService _admin;

    public SaasPlansAdminController(ISaasPlansAdminService admin) => _admin = admin;

    /// <summary>Lista planes administrables con sus <c>features</c> (incluidas/límites).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPlans(CancellationToken ct)
    {
        var rows = await _admin.ListPlansAdminAsync(ct);
        return this.ApiOk(new { plans = rows });
    }

    /// <summary>Crea un plan comercial.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSaasPlanRequest body, CancellationToken ct)
    {
        var r = await _admin.CreatePlanAsync(body, ct);
        return r.IsSuccess
            ? this.ApiOk(new { id = r.Value }, "Creado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    public sealed record SetPlanMenuBody(string? MenuConfigJson, string? MenuSidebarLayout);

    /// <summary>Obtiene el JSON de menú lateral y la preferencia de layout del plan.</summary>
    [HttpGet("{planId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPlanMenu(Guid planId, CancellationToken ct)
    {
        var r = await _admin.GetPlanMenuAsync(planId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { menuConfigJson = r.Value.MenuConfigJson, menuSidebarLayout = r.Value.MenuSidebarLayout })
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Guarda el menú lateral del plan (mismo shape que <c>SessionMenuGroupDto[]</c>). Cuerpo vacío o null limpia.</summary>
    [HttpPut("{planId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPlanMenu(Guid planId, [FromBody] SetPlanMenuBody body, CancellationToken ct)
    {
        var r = await _admin.SetPlanMenuJsonAsync(planId, body.MenuConfigJson, body.MenuSidebarLayout, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Copia menú JSON desde otro plan (transacción).</summary>
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

    /// <summary>Actualiza metadatos de un plan (nombre, precio, visibilidad, etc.).</summary>
    [HttpPut("{planId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePlan(Guid planId, [FromBody] UpdateSaasPlanRequest body, CancellationToken ct)
    {
        var r = await _admin.UpdatePlanAsync(planId, body, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Actualizado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Elimina un plan y sus vínculos a features.</summary>
    [HttpDelete("{planId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePlan(Guid planId, CancellationToken ct)
    {
        var r = await _admin.DeletePlanAsync(planId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Eliminado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    public sealed record ReorderPlansBody(IReadOnlyList<Guid> OrderedPlanIds);

    /// <summary>Reordena planes por lista de ids (orden final).</summary>
    [HttpPut("reorder")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReorderPlans([FromBody] ReorderPlansBody body, CancellationToken ct)
    {
        var r = await _admin.ReorderPlansAsync(body.OrderedPlanIds, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Orden actualizado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Marca un plan como recomendado (desmarca otros).</summary>
    [HttpPut("{planId:guid}/recommended")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetRecommended(Guid planId, CancellationToken ct)
    {
        var r = await _admin.SetRecommendedPlanAsync(planId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Plan recomendado actualizado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

}
