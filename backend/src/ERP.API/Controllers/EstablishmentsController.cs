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
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Establecimientos",
    $"perm:{SettingsPermissions.EstablishmentsView}",
    "receipt_long",
    "/settings/establishments",
    "perm:settings.group",
    28
)]
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

    /// <summary>
    /// Lista los establecimientos SRI de la empresa activa con filtros opcionales.
    /// </summary>
    /// <remarks>
    /// Filtros por query string: `activeStatus` (`all`, `active` o `inactive`; sin indicarlo solo se
    /// devuelven los activos y un valor desconocido equivale a `active`), `onlyActive`
    /// (compatibilidad), `search` (texto libre) y `branchId` (GUID de sucursal; un valor que no sea
    /// GUID se ignora). Siempre es 200: no hay errores específicos.
    /// </remarks>
    /// <response code="200">Establecimientos de la empresa activa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<EstablishmentListItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        Guid? branchId = null;
        if (
            Request.Query.TryGetValue("branchId", out var rawBranchId)
            && Guid.TryParse(rawBranchId, out var parsedBranchId)
        )
        {
            branchId = parsedBranchId;
        }

        var result = await _mediator.Send(
            new GetEstablishmentsQuery(branchId, activeFilter, search),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EstablishmentListItemDto>());
    }

    /// <summary>
    /// Devuelve los establecimientos activos de la empresa (id, código y nombre) para poblar selectores
    /// de formularios.
    /// </summary>
    /// <response code="200">Establecimientos activos de la empresa activa.</response>
    [HttpGet("lookups")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<EstablishmentLookupDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetLookups(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEstablishmentLookupsQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EstablishmentLookupDto>());
    }

    /// <summary>
    /// Crea un nuevo establecimiento SRI en la empresa activa.
    /// </summary>
    /// <remarks>
    /// El código es numérico de 1 a 3 dígitos y se normaliza a 3 con ceros a la izquierda (`1` →
    /// `001`); debe ser único en la empresa. Si se indica `branchId`, la sucursal debe existir en la
    /// empresa y estar activa. Nombre y dirección fiscal son obligatorios. Un código repetido o una
    /// sucursal inválida responden 400 (el caso de uso devuelve un fallo sin código), no 409.
    /// </remarks>
    /// <param name="command">Código, nombre, dirección fiscal, teléfono, sucursal opcional e indicador de establecimiento principal.</param>
    /// <response code="201">Establecimiento creado.</response>
    /// <response code="400">Código ya existente en la empresa, o sucursal inexistente o inactiva.</response>
    /// <response code="422">Validación de campos (código numérico de 1 a 3 dígitos, nombre y dirección obligatorios).</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<EstablishmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEstablishmentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza nombre, dirección e indicador principal de un establecimiento.
    /// </summary>
    /// <remarks>
    /// El `id` de la ruta debe coincidir con el del cuerpo. El código no se modifica. Si se marca como
    /// principal, el establecimiento principal anterior de la empresa deja de serlo. Un establecimiento
    /// inexistente responde 400, no 404 (el caso de uso devuelve un fallo sin código).
    /// </remarks>
    /// <param name="id">Id del establecimiento (debe coincidir con `command.Id`).</param>
    /// <param name="command">Nombre, dirección fiscal, teléfono e indicador principal.</param>
    /// <response code="200">Establecimiento actualizado.</response>
    /// <response code="400">El id no coincide con el cuerpo o el establecimiento no existe en la empresa activa.</response>
    /// <response code="422">Validación de campos (nombre y dirección obligatorios).</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<EstablishmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEstablishmentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva un establecimiento (baja lógica; no se elimina).
    /// </summary>
    /// <remarks>
    /// Solo se puede desactivar si no tiene puntos de emisión activos: primero hay que desactivar todos
    /// sus puntos de emisión. Esa regla y un establecimiento inexistente responden 400 (fallo sin
    /// código), no 409 ni 404.
    /// </remarks>
    /// <param name="id">Id del establecimiento.</param>
    /// <response code="200">Establecimiento desactivado.</response>
    /// <response code="400">El establecimiento no existe en la empresa activa o tiene puntos de emisión activos.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsDisable}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableEstablishmentCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reactiva un establecimiento desactivado.
    /// </summary>
    /// <remarks>
    /// No reactiva sus puntos de emisión. Un establecimiento inexistente responde 400, no 404 (fallo
    /// sin código).
    /// </remarks>
    /// <param name="id">Id del establecimiento.</param>
    /// <response code="200">Establecimiento reactivado.</response>
    /// <response code="400">El establecimiento no existe en la empresa activa.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EstablishmentsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableEstablishmentCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
