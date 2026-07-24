using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;
using ERP.Application.Modules.Company.UseCases.DisableEmissionPoint;
using ERP.Application.Modules.Company.UseCases.EnableEmissionPoint;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;
using ERP.Domain.Kernel.Permissions;

namespace ERP.API.Controllers;

[AppFeature("Puntos de Emisión", $"perm:{SettingsPermissions.EmissionPointsView}", "point_of_sale", "/settings/emission-points", "perm:settings.group", 27)]
[ApiController]
[Route("api/v1/settings/emission-points")]
[Authorize]
[Produces("application/json")]
public sealed class EmissionPointsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmissionPointsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista todos los puntos de emisión de la empresa activa.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmissionPointListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search       = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetAllEmissionPointsQuery(activeFilter, search), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EmissionPointListItemDto>());
    }

    /// <summary>Crea un nuevo punto de emisión.</summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<EmissionPointDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateEmissionPointCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Actualiza nombre, tipo de emisión e indicador de defecto de un punto de emisión.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<EmissionPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmissionPointCommand command, CancellationToken cancellationToken = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Desactiva el punto de emisión (soft delete).</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsDelete}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableEmissionPointCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva un punto de emisión desactivado.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsUpdate}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableEmissionPointCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
