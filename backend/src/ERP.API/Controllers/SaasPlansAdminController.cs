using ERP.API.Contracts;
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

    [HttpGet]
    public async Task<IActionResult> ListPlans(CancellationToken ct)
    {
        var rows = await _admin.ListPlansAdminAsync(ct);
        return Ok(new ApiResponse<object>(true, "OK", new { plans = rows }));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSaasPlanRequest body, CancellationToken ct)
    {
        var r = await _admin.CreatePlanAsync(body, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Creado", new { id = r.Value }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }

    [HttpPut("{planId:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid planId, [FromBody] UpdateSaasPlanRequest body, CancellationToken ct)
    {
        var r = await _admin.UpdatePlanAsync(planId, body, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Actualizado", new { }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }

    [HttpDelete("{planId:guid}")]
    public async Task<IActionResult> DeletePlan(Guid planId, CancellationToken ct)
    {
        var r = await _admin.DeletePlanAsync(planId, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Eliminado", new { }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }

    public sealed record ReorderPlansBody(IReadOnlyList<Guid> OrderedPlanIds);

    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderPlans([FromBody] ReorderPlansBody body, CancellationToken ct)
    {
        var r = await _admin.ReorderPlansAsync(body.OrderedPlanIds, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Orden actualizado", new { }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }

    [HttpPut("{planId:guid}/recommended")]
    public async Task<IActionResult> SetRecommended(Guid planId, CancellationToken ct)
    {
        var r = await _admin.SetRecommendedPlanAsync(planId, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Plan recomendado actualizado", new { }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }

    public sealed record ReplaceFeaturesBody(IReadOnlyList<PlanFeatureAssignDto> Features);

    [HttpPut("{planId:guid}/features")]
    public async Task<IActionResult> ReplaceFeatures(Guid planId, [FromBody] ReplaceFeaturesBody body, CancellationToken ct)
    {
        var r = await _admin.ReplacePlanFeaturesAsync(planId, body.Features, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Features actualizadas", new { }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }
}
