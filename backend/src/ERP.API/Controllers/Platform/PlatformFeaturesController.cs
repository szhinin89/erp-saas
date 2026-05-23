using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Navigation.DTOs;
using ERP.Application.Navigation.UseCases.GetAppFeatureTree;
using ERP.API.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>Platform Layer — catálogo AppFeatures para menu builder.</summary>
[ApiController]
[Route("api/platform/features")]
[Authorize(Roles = "SuperAdmin")]
[Tags("Platform")]
[Produces("application/json")]
public sealed class PlatformFeaturesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppFeatureDiscoveryService _discovery;

    public PlatformFeaturesController(IMediator mediator, AppFeatureDiscoveryService discovery)
    {
        _mediator = mediator;
        _discovery = discovery;
    }

    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppFeatureTreeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAppFeatureTreeQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var n = await _discovery.SyncFeaturesAsync(ct);
        return this.ApiOk(new { synced = n }, "OK");
    }
}
