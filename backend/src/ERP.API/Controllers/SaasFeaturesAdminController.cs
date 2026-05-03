using ERP.API.Contracts;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/superadmin/saas-features")]
[Authorize(Roles = "SuperAdmin")]
[Produces("application/json")]
public sealed class SaasFeaturesAdminController : ControllerBase
{
    private readonly ISaasPlansAdminService _admin;

    public SaasFeaturesAdminController(ISaasPlansAdminService admin) => _admin = admin;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _admin.ListFeatureDefinitionsAsync(ct);
        return Ok(new ApiResponse<object>(true, "OK", new { features = rows }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaasFeatureDefinitionRequest body, CancellationToken ct)
    {
        var r = await _admin.CreateFeatureDefinitionAsync(body, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Creada", new { id = r.Value }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }

    [HttpPut("{featureId:guid}")]
    public async Task<IActionResult> Update(Guid featureId, [FromBody] UpdateSaasFeatureDefinitionRequest body, CancellationToken ct)
    {
        var r = await _admin.UpdateFeatureDefinitionAsync(featureId, body, ct);
        return r.IsSuccess
            ? Ok(new ApiResponse<object>(true, "Actualizada", new { }))
            : BadRequest(new ApiResponse<object>(false, r.Error ?? "Error", new { }));
    }
}
