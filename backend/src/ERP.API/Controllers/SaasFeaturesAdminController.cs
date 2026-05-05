using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>CRUD de definiciones de features SaaS (<c>saas_feature_definitions</c>, solo SuperAdmin).</summary>
[ApiController]
[Route("api/superadmin/saas-features")]
[Authorize(Roles = "SuperAdmin")]
[Produces("application/json")]
public sealed class SaasFeaturesAdminController : ControllerBase
{
    private readonly ISaasPlansAdminService _admin;

    public SaasFeaturesAdminController(ISaasPlansAdminService admin) => _admin = admin;

    /// <summary>Lista todas las definiciones de features (código, permisos, límites por defecto).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _admin.ListFeatureDefinitionsAsync(ct);
        return this.ApiOk(new { features = rows });
    }

    /// <summary>Crea una definición de feature.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateSaasFeatureDefinitionRequest body, CancellationToken ct)
    {
        var r = await _admin.CreateFeatureDefinitionAsync(body, ct);
        return r.IsSuccess
            ? this.ApiOk(new { id = r.Value }, "Creada")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Actualiza una definición de feature por id.</summary>
    [HttpPut("{featureId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid featureId, [FromBody] UpdateSaasFeatureDefinitionRequest body, CancellationToken ct)
    {
        var r = await _admin.UpdateFeatureDefinitionAsync(featureId, body, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Actualizada")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    /// <summary>Elimina una definición de feature y referencias en planes, overrides, uso y menú.</summary>
    [HttpDelete("{featureId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Delete(Guid featureId, CancellationToken ct) => DeleteFeatureDefinitionAsync(featureId, ct);

    /// <summary>Misma eliminación vía POST (p. ej. proxies que bloquean DELETE con 405).</summary>
    [HttpPost("{featureId:guid}/delete")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> DeletePost(Guid featureId, CancellationToken ct) => DeleteFeatureDefinitionAsync(featureId, ct);

    private async Task<IActionResult> DeleteFeatureDefinitionAsync(Guid featureId, CancellationToken ct)
    {
        var r = await _admin.DeleteFeatureDefinitionAsync(featureId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Eliminada")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
