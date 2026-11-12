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
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Puntos de emisión",
    $"perm:{SettingsPermissions.EmissionPointsView}",
    "point_of_sale",
    "/settings/emission-points",
    "perm:settings.group",
    27
)]
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

    /// <summary>
    /// Lista los puntos de emisión SRI de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Filtros por query string: `activeStatus` (`all`, `active` o `inactive`; sin indicarlo solo se
    /// devuelven los activos y un valor desconocido equivale a `active`), `onlyActive` (compatibilidad)
    /// y `search` (texto libre). Cada elemento incluye el código de su establecimiento. Siempre es 200:
    /// no hay errores específicos.
    /// </remarks>
    /// <response code="200">Puntos de emisión de la empresa activa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<EmissionPointListItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(
            new GetAllEmissionPointsQuery(activeFilter, search),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<EmissionPointListItemDto>());
    }

    /// <summary>
    /// Crea un punto de emisión dentro de un establecimiento de la empresa activa.
    /// </summary>
    /// <remarks>
    /// El establecimiento debe existir en la empresa y estar activo. El código es numérico de 1 a 3
    /// dígitos, se normaliza a 3 con ceros a la izquierda y debe ser único dentro del establecimiento.
    /// El tipo de emisión es `Electronic` o `Physical`. Con `isDefault = true` el punto por defecto
    /// anterior del establecimiento deja de serlo. Este endpoint no crea ni modifica secuenciales: los
    /// secuenciales se gestionan en `document-sequences`. Un código repetido o un establecimiento
    /// inválido responden 400 (fallo sin código), no 409.
    /// </remarks>
    /// <param name="command">Establecimiento, código, nombre, tipo de emisión e indicador de punto por defecto.</param>
    /// <response code="201">Punto de emisión creado.</response>
    /// <response code="400">El establecimiento no existe o está inactivo, o el código ya existe en el establecimiento.</response>
    /// <response code="422">Validación de campos (código numérico de 1 a 3 dígitos, tipo de emisión válido, longitud del nombre).</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<EmissionPointDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmissionPointCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza nombre, tipo de emisión e indicador de defecto de un punto de emisión.
    /// </summary>
    /// <remarks>
    /// El `id` de la ruta debe coincidir con el del cuerpo. El código y el establecimiento no se
    /// modifican. Si se marca como punto por defecto, el anterior del mismo establecimiento deja de
    /// serlo y el cambio queda registrado. Un punto inexistente responde 400, no 404 (fallo sin
    /// código).
    /// </remarks>
    /// <param name="id">Id del punto de emisión (debe coincidir con `command.Id`).</param>
    /// <param name="command">Nombre, tipo de emisión e indicador de punto por defecto.</param>
    /// <response code="200">Punto de emisión actualizado.</response>
    /// <response code="400">El id no coincide con el cuerpo o el punto de emisión no existe en la empresa activa.</response>
    /// <response code="422">Validación de campos (tipo de emisión válido, longitud del nombre).</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<EmissionPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmissionPointCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Desactiva un punto de emisión (baja lógica; no se elimina).
    /// </summary>
    /// <remarks>
    /// Un punto inexistente responde 400, no 404 (el caso de uso devuelve un fallo sin código).
    /// </remarks>
    /// <param name="id">Id del punto de emisión.</param>
    /// <response code="200">Punto de emisión desactivado.</response>
    /// <response code="400">El punto de emisión no existe en la empresa activa.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsDelete}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableEmissionPointCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Reactiva un punto de emisión desactivado.
    /// </summary>
    /// <remarks>
    /// Un punto inexistente responde 400, no 404 (el caso de uso devuelve un fallo sin código).
    /// </remarks>
    /// <param name="id">Id del punto de emisión.</param>
    /// <response code="200">Punto de emisión reactivado.</response>
    /// <response code="400">El punto de emisión no existe en la empresa activa.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.EmissionPointsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableEmissionPointCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
