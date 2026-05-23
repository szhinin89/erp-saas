using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Filters;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.SuperAdminSubscribers;
using ERP.Application.Admin;
using ERP.Application.Admin.UseCases.SuperAdminGlobal;
using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Endpoints globales para SuperAdmin (multi-empresa).
/// Estos endpoints ignoran el contexto del tenant porque el SuperAdmin opera a nivel sistema.
/// </summary>
[ApiController]
[AppFeature("SuperAdmin Global", "perm:superadmin.global.admin", "🧩", null, null, 982, IsVisibleInMenu = false, IsSuperAdmin = true)]
[Route("api/superadmin")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
[DeprecatedApi("/api/platform/subscribers")]
public sealed class SuperAdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public SuperAdminController(IMediator mediator) => _mediator = mediator;

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

    [HttpGet("plans")]
    [DeprecatedApi("/api/platform/plans")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlansCatalog(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminPlansCatalogQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [Obsolete("Legacy SuperAdmin analytics route. Prefer GET /api/platform/subscribers for platform CRUD alignment.")]
    [HttpGet("subscribers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminSubscribersQuery(), ct);
        if (!result.IsSuccess)
            return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SuperAdminSubscriberItemDto>());

        return this.ApiOk(new { subscribers = result.Value });
    }

    [HttpGet("metrics")]
    [DeprecatedApi("/api/platform/metrics")]
    [ProducesResponseType(typeof(ApiResponse<SuperAdminMetricsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminMetricsQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("growth-analytics")]
    [DeprecatedApi("/api/platform/metrics/growth-analytics")]
    [ProducesResponseType(typeof(ApiResponse<GrowthAnalyticsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGrowthAnalytics(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminGrowthAnalyticsQuery(from, to, granularity), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("growth-analytics-monetary")]
    [DeprecatedApi("/api/platform/metrics/growth-analytics-monetary")]
    [ProducesResponseType(typeof(ApiResponse<GrowthMonetaryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGrowthMonetary(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? granularity,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminGrowthMonetaryQuery(from, to, granularity), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("navigation-menu")]
    [DeprecatedApi("/api/platform/navigation-menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNavigationMenu(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminNavigationMenuQuery(), ct);
        return result.IsSuccess
            ? this.ApiOk(new { menu = result.Value })
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPut("navigation-menu/groups/reorder")]
    [DeprecatedApi("/api/platform/navigation-menu/groups/reorder")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderNavigationGroups(
        [FromBody] ReorderNavigationGroupsRequest body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderSuperAdminNavigationGroupsCommand(body.OrderedGroupIds), ct);
        return result.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPut("navigation-menu/items/reorder-levels")]
    [DeprecatedApi("/api/platform/navigation-menu/items/reorder-levels")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderNavigationItemLevels(
        [FromBody] ReorderNavigationItemLevelsRequest body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderSuperAdminNavigationItemLevelsCommand(body.Levels), ct);
        return result.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPost("navigation-menu/items")]
    [DeprecatedApi("/api/platform/navigation-menu/items")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNavigationMenuItem([FromBody] CreateNavItemRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSuperAdminNavigationMenuItemCommand(body), ct);
        return result.IsSuccess
            ? this.ApiOk(new { id = result.Value }, "Creado")
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPut("navigation-menu/items/{itemId:guid}")]
    [DeprecatedApi("/api/platform/navigation-menu/items/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateNavigationMenuItem(
        Guid itemId,
        [FromBody] UpdateNavItemRequest body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSuperAdminNavigationMenuItemCommand(itemId, body), ct);
        return result.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpDelete("navigation-menu/items/{itemId:guid}")]
    [DeprecatedApi("/api/platform/navigation-menu/items/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteNavigationMenuItem(Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteSuperAdminNavigationMenuItemCommand(itemId), ct);
        return result.IsSuccess
            ? this.ApiOk(new { }, "Eliminado")
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpDelete("users/{userId:guid}/sessions")]
    [DeprecatedApi("/api/platform/users/{userId}/sessions")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeUserSessions(
        Guid userId,
        [FromQuery] Guid subscriberId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new RevokeSuperAdminUserSessionsCommand(userId, subscriberId), ct);
        return result.IsSuccess
            ? this.ApiOk(result.Value!, "OK")
            : this.ApiBadRequest(result.Error ?? "Error");
    }
}
