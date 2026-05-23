using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>
/// Platform Layer — configuración por suscriptor (global, módulo, feature).
/// Canonical successor of /api/platform/config.
/// </summary>
[ApiController]
[Route("api/platform/config")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly ICurrentUser _currentUser;

    public PlatformConfigController(IConfigService configService, ICurrentUser currentUser)
    {
        _configService = configService;
        _currentUser = currentUser;
    }

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

    [HttpGet("{subscriberId:guid}/global")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGlobal([FromRoute] Guid subscriberId, CancellationToken ct)
    {
        var rows = await _configService.ListGlobalAsync(subscriberId, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{subscriberId:guid}/global")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    [HttpGet("{subscriberId:guid}/module/{module}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListModule([FromRoute] Guid subscriberId, [FromRoute] string module, CancellationToken ct)
    {
        var rows = await _configService.ListModuleAsync(subscriberId, module, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{subscriberId:guid}/module/{module}")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertModule(
        [FromRoute] Guid subscriberId,
        [FromRoute] string module,
        [FromBody] UpsertScopedConfigBody body,
        CancellationToken ct)
    {
        try
        {
            var row = await _configService.UpsertModuleAsync(subscriberId, module, body.Key, body.Value, body.DataType, CurrentUserIdOrEmpty(), ct);
            return this.ApiOk(row);
        }
        catch (ArgumentException ex)
        {
            return this.ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete("{subscriberId:guid}/module/{module}/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteModule(
        [FromRoute] Guid subscriberId,
        [FromRoute] string module,
        [FromRoute] string key,
        CancellationToken ct)
    {
        _ = await _configService.DeleteModuleAsync(subscriberId, module, key, ct);
        return this.ApiOk(new { });
    }

    [HttpGet("{subscriberId:guid}/feature/{feature}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFeature([FromRoute] Guid subscriberId, [FromRoute] string feature, CancellationToken ct)
    {
        var rows = await _configService.ListFeatureAsync(subscriberId, feature, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{subscriberId:guid}/feature/{feature}")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertFeature(
        [FromRoute] Guid subscriberId,
        [FromRoute] string feature,
        [FromBody] UpsertScopedConfigBody body,
        CancellationToken ct)
    {
        try
        {
            var row = await _configService.UpsertFeatureAsync(subscriberId, feature, body.Key, body.Value, body.DataType, CurrentUserIdOrEmpty(), ct);
            return this.ApiOk(row);
        }
        catch (ArgumentException ex)
        {
            return this.ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete("{subscriberId:guid}/feature/{feature}/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFeature(
        [FromRoute] Guid subscriberId,
        [FromRoute] string feature,
        [FromRoute] string key,
        CancellationToken ct)
    {
        _ = await _configService.DeleteFeatureAsync(subscriberId, feature, key, ct);
        return this.ApiOk(new { });
    }

    private Guid CurrentUserIdOrEmpty() =>
        _currentUser.UserId == Guid.Empty ? Guid.Empty : _currentUser.UserId;

    public sealed record UpsertGlobalConfigBody(string Key, string Value, string DataType = "string");
    public sealed record UpsertScopedConfigBody(string Key, string Value, string DataType = "string");
}
