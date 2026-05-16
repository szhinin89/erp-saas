using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Admin;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[AppFeature("SuperAdmin Config", "perm:superadmin.config.admin", "🧩", null, null, 991, IsVisibleInMenu = false, IsSuperAdmin = true)]
[Route("api/superadmin/config")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly ICurrentUser _currentUser;

    public SuperAdminConfigController(IConfigService configService, ICurrentUser currentUser)
    {
        _configService = configService;
        _currentUser = currentUser;
    }

    [HttpGet("{tenantId:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<ResolvedConfigValueDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve([FromRoute] Guid tenantId, [FromQuery] string key, [FromQuery] string? module, [FromQuery] string? feature, CancellationToken ct)
    {
        var value = await _configService.GetValueAsync(tenantId, key, module, feature, null, ct);
        return this.ApiOk(value);
    }

    [HttpGet("{tenantId:guid}/global")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGlobal([FromRoute] Guid tenantId, CancellationToken ct)
    {
        var rows = await _configService.ListGlobalAsync(tenantId, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{tenantId:guid}/global")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertGlobal([FromRoute] Guid tenantId, [FromBody] UpsertGlobalConfigBody body, CancellationToken ct)
    {
        try
        {
            var row = await _configService.UpsertGlobalAsync(tenantId, body.Key, body.Value, body.DataType, CurrentUserIdOrEmpty(), ct);
            return this.ApiOk(row);
        }
        catch (ArgumentException ex)
        {
            return this.ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete("{tenantId:guid}/global/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteGlobal([FromRoute] Guid tenantId, [FromRoute] string key, CancellationToken ct)
    {
        _ = await _configService.DeleteGlobalAsync(tenantId, key, ct);
        return this.ApiOk(new { });
    }

    [HttpGet("{tenantId:guid}/module/{module}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListModule([FromRoute] Guid tenantId, [FromRoute] string module, CancellationToken ct)
    {
        var rows = await _configService.ListModuleAsync(tenantId, module, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{tenantId:guid}/module/{module}")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertModule([FromRoute] Guid tenantId, [FromRoute] string module, [FromBody] UpsertScopedConfigBody body, CancellationToken ct)
    {
        try
        {
            var row = await _configService.UpsertModuleAsync(tenantId, module, body.Key, body.Value, body.DataType, CurrentUserIdOrEmpty(), ct);
            return this.ApiOk(row);
        }
        catch (ArgumentException ex)
        {
            return this.ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete("{tenantId:guid}/module/{module}/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteModule([FromRoute] Guid tenantId, [FromRoute] string module, [FromRoute] string key, CancellationToken ct)
    {
        _ = await _configService.DeleteModuleAsync(tenantId, module, key, ct);
        return this.ApiOk(new { });
    }

    [HttpGet("{tenantId:guid}/feature/{feature}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConfigEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFeature([FromRoute] Guid tenantId, [FromRoute] string feature, CancellationToken ct)
    {
        var rows = await _configService.ListFeatureAsync(tenantId, feature, ct);
        return this.ApiOk(rows);
    }

    [HttpPut("{tenantId:guid}/feature/{feature}")]
    [ProducesResponseType(typeof(ApiResponse<ConfigEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertFeature([FromRoute] Guid tenantId, [FromRoute] string feature, [FromBody] UpsertScopedConfigBody body, CancellationToken ct)
    {
        try
        {
            var row = await _configService.UpsertFeatureAsync(tenantId, feature, body.Key, body.Value, body.DataType, CurrentUserIdOrEmpty(), ct);
            return this.ApiOk(row);
        }
        catch (ArgumentException ex)
        {
            return this.ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete("{tenantId:guid}/feature/{feature}/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFeature([FromRoute] Guid tenantId, [FromRoute] string feature, [FromRoute] string key, CancellationToken ct)
    {
        _ = await _configService.DeleteFeatureAsync(tenantId, feature, key, ct);
        return this.ApiOk(new { });
    }

    private Guid CurrentUserIdOrEmpty()
    {
        return _currentUser.UserId == Guid.Empty ? Guid.Empty : _currentUser.UserId;
    }

    public sealed record UpsertGlobalConfigBody(string Key, string Value, string DataType = "string");
    public sealed record UpsertScopedConfigBody(string Key, string Value, string DataType = "string");
}

