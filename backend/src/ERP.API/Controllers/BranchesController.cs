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

[AppFeature(
    "Sucursales",
    $"perm:{SettingsPermissions.BranchesView}",
    "🏢",
    "/settings/branches",
    "perm:settings.group",
    30
)]
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

    /// <summary>
    /// Lista las sucursales de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Filtros por query string: `activeStatus` (`all`, `active` o `inactive`; sin indicarlo solo se
    /// devuelven las activas y un valor desconocido equivale a `active`), `onlyActive` (compatibilidad:
    /// `true` solo activas, `false` todas) y `search` (texto libre; vacío = sin filtro). Siempre es
    /// 200: no hay errores específicos.
    /// </remarks>
    /// <response code="200">Sucursales de la empresa activa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<BranchListItemDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(
            new GetBranchesQuery(activeFilter, search),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<BranchListItemDto>());
    }

    /// <summary>
    /// Obtiene el detalle de una sucursal de la empresa activa.
    /// </summary>
    /// <param name="id">Id de la sucursal.</param>
    /// <response code="200">Detalle de la sucursal.</response>
    /// <response code="404">La sucursal no existe en la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesView}")]
    [ProducesResponseType(typeof(ApiResponse<BranchDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBranchByIdQuery(id), cancellationToken);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>
    /// Crea una sucursal en la empresa activa.
    /// </summary>
    /// <remarks>
    /// El código (`SUC-aaaa-…`) lo genera el sistema. Con `isMainBranch = true` la sucursal principal
    /// anterior de la empresa deja de serlo (cada cambio queda en el registro de cambios de
    /// configuración). Con `isActive = false` se crea deshabilitada. La ubicación geográfica debe ser
    /// coherente: no se admite provincia sin país, cantón sin provincia ni parroquia sin cantón, y cada
    /// nivel debe pertenecer al anterior. Registra la actividad `branch.create`.
    /// </remarks>
    /// <param name="command">Datos de la sucursal; nombre y dirección son obligatorios (máx. 100 y 200 caracteres).</param>
    /// <response code="201">Sucursal creada.</response>
    /// <response code="400">Ubicación geográfica inválida o inconsistente.</response>
    /// <response code="422">Validación de campos (obligatorios, longitudes, correos).</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesCreate}")]
    [ProducesResponseType(typeof(ApiResponse<BranchListItemDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBranchCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza los datos de una sucursal.
    /// </summary>
    /// <remarks>
    /// El `id` de la ruta debe coincidir con el del cuerpo. `isActive` habilita o deshabilita la
    /// sucursal y `isMainBranch = true` la convierte en la principal (la anterior deja de serlo y el
    /// cambio queda en el registro de configuración). Aplican las mismas reglas de ubicación geográfica
    /// que al crear. Una sucursal inexistente responde 400, no 404 (el caso de uso devuelve un fallo
    /// sin código).
    /// </remarks>
    /// <param name="id">Id de la sucursal (debe coincidir con `command.Id`).</param>
    /// <param name="command">Datos completos de la sucursal.</param>
    /// <response code="200">Sucursal actualizada.</response>
    /// <response code="400">El id no coincide con el cuerpo, la sucursal no existe en la empresa activa, faltan nombre o dirección o la ubicación es inválida.</response>
    /// <response code="422">Validación de campos (obligatorios, longitudes, correos).</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BranchListItemDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBranchCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Deshabilita una sucursal (baja lógica; no se elimina).
    /// </summary>
    /// <remarks>
    /// Registra la actividad `branch.disable`. Una sucursal inexistente en la empresa activa responde
    /// 400, no 404 (el caso de uso devuelve un fallo sin código).
    /// </remarks>
    /// <param name="id">Id de la sucursal.</param>
    /// <response code="200">Sucursal deshabilitada.</response>
    /// <response code="400">La sucursal no existe en la empresa activa.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesDelete}")]
    [ProducesResponseType(typeof(ApiResponse<BranchListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DisableBranchCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Habilita una sucursal previamente deshabilitada.
    /// </summary>
    /// <remarks>
    /// Registra la actividad `branch.enable`. Una sucursal inexistente en la empresa activa responde
    /// 400, no 404 (el caso de uso devuelve un fallo sin código).
    /// </remarks>
    /// <param name="id">Id de la sucursal.</param>
    /// <response code="200">Sucursal habilitada.</response>
    /// <response code="400">La sucursal no existe en la empresa activa.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BranchesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BranchListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new EnableBranchCommand(id), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
