using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Application.Modules.Branches.UseCases.CreateBranch;
using ERP.Application.Modules.Branches.UseCases.DisableBranch;
using ERP.Application.Modules.Branches.UseCases.EnableBranch;
using ERP.Application.Modules.Branches.UseCases.GetBranchById;
using ERP.Application.Modules.Branches.UseCases.GetBranches;
using ERP.Application.Modules.Branches.UseCases.UpdateBranch;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Sucursales", $"perm:{SettingsPermissions.BranchesView}", "🏢", "/settings/branches", "perm:settings.group", 30)]
[ApiController]
[Route("api/v1/settings/branches")]
[Authorize]
[Produces("application/json")]
public sealed class BranchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BranchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BranchListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetBranchesQuery(activeFilter, search), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<BranchListItemDto>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesView}")]
    [ProducesResponseType(typeof(ApiResponse<BranchDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBranchByIdQuery(id), cancellationToken);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesCreate}")]
    [ProducesResponseType(typeof(ApiResponse<BranchListItemDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateBranchCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BranchListItemDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchCommand command, CancellationToken cancellationToken = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesDelete}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableBranchCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesUpdate}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableBranchCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
