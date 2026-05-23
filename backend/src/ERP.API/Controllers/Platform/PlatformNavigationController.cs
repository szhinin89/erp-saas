using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Admin.UseCases.SuperAdminGlobal;
using ERP.Application.Navigation.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Platform Layer — menú ERP global editable por SuperAdmin.</summary>
[ApiController]
[Route("api/platform/navigation-menu")]
[Authorize(Roles = "SuperAdmin")]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformNavigationController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformNavigationController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSuperAdminNavigationMenuQuery(), ct);
        return result.IsSuccess
            ? this.ApiOk(new { menu = result.Value })
            : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPut("groups/reorder")]
    public async Task<IActionResult> ReorderGroups([FromBody] ReorderNavigationGroupsRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderSuperAdminNavigationGroupsCommand(body.OrderedGroupIds), ct);
        return result.IsSuccess ? this.ApiOk(new { }, "Guardado") : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPut("items/reorder-levels")]
    public async Task<IActionResult> ReorderItemLevels([FromBody] ReorderNavigationItemLevelsRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderSuperAdminNavigationItemLevelsCommand(body.Levels), ct);
        return result.IsSuccess ? this.ApiOk(new { }, "Guardado") : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] CreateNavItemRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSuperAdminNavigationMenuItemCommand(body), ct);
        return result.IsSuccess ? this.ApiOk(new { id = result.Value }, "Creado") : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateNavItemRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSuperAdminNavigationMenuItemCommand(itemId, body), ct);
        return result.IsSuccess ? this.ApiOk(new { }, "Guardado") : this.ApiBadRequest(result.Error ?? "Error");
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteSuperAdminNavigationMenuItemCommand(itemId), ct);
        return result.IsSuccess ? this.ApiOk(new { }, "Eliminado") : this.ApiBadRequest(result.Error ?? "Error");
    }
}
