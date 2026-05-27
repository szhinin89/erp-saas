using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.CreateEstablishment;
using ERP.Application.Modules.Company.UseCases.DisableEstablishment;
using ERP.Application.Modules.Company.UseCases.EnableEstablishment;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Application.Modules.Company.UseCases.UpdateEstablishment;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/settings/branches/{branchId:guid}/establishments")]
[Authorize]
[Produces("application/json")]
public sealed class EstablishmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EstablishmentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:settings.branches.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EstablishmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid branchId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEstablishmentsByBranchQuery(branchId), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EstablishmentDto>());
    }

    [HttpPost]
    [Authorize(Policy = "perm:settings.branches.create")]
    [ProducesResponseType(typeof(ApiResponse<EstablishmentDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid branchId, [FromBody] CreateEstablishmentCommand command, CancellationToken ct = default)
    {
        if (branchId != command.BranchId)
            return this.ApiBadRequest("El branchId de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:settings.branches.update")]
    [ProducesResponseType(typeof(ApiResponse<EstablishmentDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid branchId, Guid id, [FromBody] UpdateEstablishmentCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:settings.branches.delete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableEstablishmentCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:settings.branches.update")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enable(Guid branchId, Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableEstablishmentCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
