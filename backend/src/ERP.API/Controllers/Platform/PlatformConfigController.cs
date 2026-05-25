using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

[ApiController]
[Route("api/platform/config")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformConfigController : ControllerBase
{
    private readonly IConfigService _configService;

    public PlatformConfigController(IConfigService configService) => _configService = configService;

    [HttpGet("{subscriberId:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<ResolvedConfigValueDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(
        [FromRoute] Guid subscriberId,
        [FromQuery] string key,
        [FromQuery] string? module,
        [FromQuery] string? feature,
        CancellationToken ct)
    {
        var value = await _configService.GetValueAsync(subscriberId, key, module, feature, null, ct);
        return this.ApiOk(value);
    }
}
