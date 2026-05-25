using ERP.API.Contracts;
using ERP.API.Contracts.Platform;
using ERP.API.Extensions;
using ERP.Application.Admin;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

[ApiController]
[Route("api/platform/config")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformGlobalConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly ICurrentUser _currentUser;

    public PlatformGlobalConfigController(IConfigService configService, ICurrentUser currentUser)
    {
        _configService = configService;
        _currentUser = currentUser;
    }

    [HttpGet("{subscriberId:guid}/global")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGlobal([FromRoute] Guid subscriberId, CancellationToken ct)
    {
        var rows = await _configService.ListGlobalAsync(subscriberId, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{subscriberId:guid}/global")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertGlobal([FromRoute] Guid subscriberId, [FromBody] UpsertGlobalConfigBody body, CancellationToken ct)
    {
        try
        {
            var row = await _configService.UpsertGlobalAsync(subscriberId, body.Key, body.Value, body.DataType, CurrentUserIdOrEmpty(), ct);
            return this.ApiOk(row);
        }
        catch (ArgumentException ex)
        {
            return this.ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete("{subscriberId:guid}/global/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteGlobal([FromRoute] Guid subscriberId, [FromRoute] string key, CancellationToken ct)
    {
        _ = await _configService.DeleteGlobalAsync(subscriberId, key, ct);
        return this.ApiOk(new { });
    }

    private Guid CurrentUserIdOrEmpty() =>
        _currentUser.UserId == Guid.Empty ? Guid.Empty : _currentUser.UserId;
}
