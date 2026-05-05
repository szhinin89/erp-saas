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
[Authorize(Roles = "SuperAdmin")]
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

    /// <summary>
    /// Cuerpo para reemplazar por completo las features de un plan.
    /// Debe incluir todas las definiciones existentes en <c>saas_feature_definitions</c> (una fila por featureId).
    /// </summary>
    public sealed record ReplaceFeaturesBody(IReadOnlyList<PlanFeatureAssignDto> Features);

    /// <summary>
    /// Reemplaza el conjunto de features incluidas en un plan (borra vínculos previos e inserta los enviados).
    /// Usado por la UI <c>Empresas → Plan ↔ menú</c> y el catálogo en <c>Plan y módulos</c>.
    /// </summary>
    [HttpPut("{planId:guid}/features")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReplaceFeatures(Guid planId, [FromBody] ReplaceFeaturesBody body, CancellationToken ct)
    {
        var r = await _admin.ReplacePlanFeaturesAsync(planId, body.Features, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Features actualizadas")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
