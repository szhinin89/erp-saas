using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.CreateEmissionPoint;
using ERP.Application.Modules.Company.UseCases.DisableEmissionPoint;
using ERP.Application.Modules.Company.UseCases.EnableEmissionPoint;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/settings/establishments/{establishmentId:guid}/emission-points")]
[Authorize]
[Produces("application/json")]
public sealed class EmissionPointsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmissionPointsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:settings.branches.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmissionPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid establishmentId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEmissionPointsByEstablishmentQuery(establishmentId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EmissionPointDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:settings.branches.create")]
    [ProducesResponseType(typeof(ApiResponse<EmissionPointDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid establishmentId, [FromBody] CreateEmissionPointCommand command, CancellationToken ct = default)
    {
        if (establishmentId != command.EstablishmentId)
            return this.ApiBadRequest("El establishmentId de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:settings.branches.update")]
    [ProducesResponseType(typeof(ApiResponse<EmissionPointDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid establishmentId, Guid id, [FromBody] UpdateEmissionPointCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:settings.branches.delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable(Guid establishmentId, Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableEmissionPointCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:settings.branches.update")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enable(Guid establishmentId, Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableEmissionPointCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
