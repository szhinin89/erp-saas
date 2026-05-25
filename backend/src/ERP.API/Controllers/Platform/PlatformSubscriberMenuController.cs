using ERP.API.Contracts;
using ERP.API.Contracts.Platform;
using ERP.API.Extensions;
using ERP.Application.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

[ApiController]
[Route("api/platform/subscribers")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform")]
public sealed class PlatformSubscriberMenuController : ControllerBase
{
    private readonly ISubscriberMenuAdminService _subscriberMenuAdmin;

    public PlatformSubscriberMenuController(ISubscriberMenuAdminService subscriberMenuAdmin)
        => _subscriberMenuAdmin = subscriberMenuAdmin;

    [HttpGet("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(Guid subscriberId, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.GetResolvedMenuForSubscriberAsync(subscriberId, ct);
        if (!r.IsSuccess)
            return this.ApiBadRequest(r.Error ?? "Error");
        var v = r.Value!;
        return this.ApiOk(new
        {
            menu = v.Menu,
            hasCustomMenu = v.HasCustomMenu,
            usedPlanMenu = v.UsedPlanMenu,
            usedGlobalFallback = v.UsedGlobalFallback,
        });
    }

    [HttpPut("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutMenu(Guid subscriberId, [FromBody] SubscriberMenuPutBody body, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.UpsertSubscriberCustomMenuAsync(subscriberId, body.MenuConfigJson, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Guardado")
            : this.ApiBadRequest(r.Error ?? "Error");
    }

    [HttpDelete("{subscriberId:guid}/menu")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMenu(Guid subscriberId, CancellationToken ct)
    {
        var r = await _subscriberMenuAdmin.DeleteSubscriberCustomMenuAsync(subscriberId, ct);
        return r.IsSuccess
            ? this.ApiOk(new { }, "Restablecido")
            : this.ApiBadRequest(r.Error ?? "Error");
    }
}
