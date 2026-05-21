using ERP.API.Contracts;
using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.API.Services;
using ERP.Application.Common;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Navigation.UseCases.GetAppFeatureTree;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[AppFeature("SuperAdmin AppFeatures", "perm:superadmin.AppFeatures.admin", "ðŸ§©", null, null, 984, IsVisibleInMenu = false, IsSuperAdmin = true)]
[Route("api/superadmin/AppFeatures")]
[Authorize(Policy = "GlobalSuperAdmin")]
[Produces("application/json")]
public sealed class SuperAdminAppFeaturesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppFeatureDiscoveryService _discovery;

    public SuperAdminAppFeaturesController(IMediator mediator, AppFeatureDiscoveryService discovery)
    {
        _mediator = mediator;
        _discovery = discovery;
    }

    /// <summary>Syncs the catalog from <c>[AppFeature]</c> attributes on controllers/actions.</summary>
    [HttpPost("sincronizar")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var n = await _discovery.SyncFeaturesAsync(ct);
        return this.ApiOk(new { synced = n }, "OK");
    }

    [HttpGet("arbol")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppFeatureTreeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAppFeatureTreeQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }
}
