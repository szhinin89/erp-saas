using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.CreateEstablishment;
using ERP.Application.Modules.Company.UseCases.DisableEstablishment;
using ERP.Application.Modules.Company.UseCases.EnableEstablishment;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Application.Modules.Company.UseCases.UpdateEstablishment;
using ERP.Domain.Kernel.Permissions;

namespace ERP.API.Controllers;

[AppFeature("Establecimientos SRI", $"perm:{SettingsPermissions.EstablishmentsView}", "receipt_long", "/settings/establishments", "perm:settings.group", 28)]
[ApiController]
[Route("api/v1/settings/establishments")]
[Authorize]
[Produces("application/json")]
public sealed class EstablishmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EstablishmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lista los establecimientos de la empresa activa con filtros opcionales.</summary>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EstablishmentListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search       = CatalogQueryParameters.ParseSearch(Request.Query);
        Guid? branchId   = null;
        if (Request.Query.TryGetValue("branchId", out var rawBranchId)
            && Guid.TryParse(rawBranchId, out var parsedBranchId))
        {
            branchId = parsedBranchId;
        }

        var result = await _mediator.Send(new GetEstablishmentsQuery(branchId, activeFilter, search), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EstablishmentListItemDto>());
    }

    /// <summary>Devuelve los establecimientos activos de la empresa — para poblar el selector en formularios.</summary>
    [HttpGet("lookups")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EstablishmentLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookups(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEstablishmentLookupsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EstablishmentLookupDto>());
    }

    /// <summary>Crea un nuevo establecimiento SRI.</summary>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<EstablishmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateEstablishmentCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Actualiza nombre, dirección e indicador principal de un establecimiento.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<EstablishmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEstablishmentCommand command, CancellationToken cancellationToken = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Desactiva el establecimiento (soft delete).</summary>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsDisable}")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableEstablishmentCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva un establecimiento desactivado.</summary>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsUpdate}")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableEstablishmentCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
