using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin.UseCases.PlatformGlobal;
using ERP.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>
/// Platform Layer — parámetros de instancia (cuotas de despliegue).
/// Sucesor de <c>GET/PUT /api/platform/instance-quota</c> (Phase 4).
/// </summary>
[ApiController]
[Route("api/platform/settings")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformSettingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("instance-quota")]
    [ProducesResponseType(typeof(ApiResponse<InstanceQuotaFileModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstanceQuota(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInstanceQuotaQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPut("instance-quota")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutInstanceQuota([FromBody] InstanceQuotaFileModel body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateInstanceQuotaCommand(body), ct);
        return result.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(result.Error ?? "Error");
    }
}
