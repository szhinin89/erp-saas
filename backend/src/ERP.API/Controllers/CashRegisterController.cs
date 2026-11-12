using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Cajas registradoras (<c>CashRegister</c>): recurso de configuración por sucursal, con punto de
/// emisión, bodega y cliente por defecto y cuenta contable. No se eliminan: se habilitan o
/// deshabilitan.
/// </summary>
[ApiController]
[Route("api/v1/cash-registers")]
[Authorize]
[Produces("application/json")]
public sealed class CashRegisterController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashRegisterController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista las cajas de la sucursal activa.</summary>
    /// <param name="activeOnly">Si es <c>true</c> solo devuelve cajas activas; si es <c>false</c> solo inactivas; omitido devuelve todas.</param>
    /// <response code="200">Cajas de la sucursal activa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CashRegisterDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetByCurrentBranch(
        [FromQuery] bool? activeOnly,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCashRegistersByCurrentBranchQuery(activeOnly), ct),
            "OK"
        );

    /// <summary>Listado de administración — empresa completa, todas las sucursales.</summary>
    /// <param name="activeOnly">Si es <c>true</c> solo devuelve cajas activas; si es <c>false</c> solo inactivas; omitido devuelve todas.</param>
    /// <param name="search">Texto opcional; filtra sin distinguir mayúsculas por código o nombre de la caja o por nombre de la sucursal.</param>
    /// <response code="200">Cajas de la empresa, de todas las sucursales.</response>
    [HttpGet("all")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CashRegisterDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? activeOnly,
        [FromQuery] string? search,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetAllCashRegistersQuery(activeOnly, search), ct),
            "OK"
        );

    /// <summary>Puntos de emisión activos de una Sucursal — selector cascada del formulario de Caja.</summary>
    /// <param name="branchId">Sucursal de la que se listan los puntos de emisión.</param>
    /// <response code="200">Puntos de emisión activos de la sucursal.</response>
    [HttpGet("emission-point-lookups")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<EmissionPointLookupForBranchDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetEmissionPointLookups(
        [FromQuery] Guid branchId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetEmissionPointLookupsByBranchQuery(branchId), ct),
            "OK"
        );

    /// <summary>Obtiene una caja por id, con indicador de si ya tiene historial operativo.</summary>
    /// <param name="id">Id de la caja.</param>
    /// <response code="200">Caja encontrada.</response>
    /// <response code="404">La caja no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashRegisterByIdQuery(id), ct));

    /// <summary>
    /// READINESS-2B-3 — si la caja de la sucursal activa puede iniciar la jornada de ventas con su
    /// punto de emisión. Mismo permiso que la apertura; la apertura lo vuelve a evaluar.
    /// </summary>
    /// <remarks>
    /// Solo lectura: no abre ninguna sesión. Devuelve <c>canOpen</c> y la lista de requisitos
    /// pendientes del punto de emisión.
    /// </remarks>
    /// <param name="id">Id de la caja.</param>
    /// <response code="200">Estado de habilitación de la caja para abrir sesión.</response>
    /// <response code="404">La caja no existe o no pertenece a la sucursal activa.</response>
    [HttpGet("{id:guid}/opening-readiness")]
    [Authorize(Policy = $"perm:{CajaPermissions.Open}")]
    [ProducesResponseType(
        typeof(ApiResponse<CashRegisterOpeningReadinessDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOpeningReadiness(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCashRegisterOpeningReadinessQuery(id), ct));

    /// <summary>Crea una caja en una sucursal de la empresa activa.</summary>
    /// <remarks>
    /// La sucursal la elige el administrador en el cuerpo; empresa y tenant salen del contexto
    /// autenticado. El punto de emisión es obligatorio y debe pertenecer a la sucursal. La bodega
    /// por defecto, si se indica, debe pertenecer a la sucursal; el cliente por defecto debe existir
    /// y la cuenta contable debe estar activa y ser imputable. El código debe ser único dentro de la
    /// sucursal.
    /// </remarks>
    /// <param name="command">Datos de la caja.</param>
    /// <response code="201">Caja creada.</response>
    /// <response code="422">Datos inválidos: sucursal inexistente o inactiva, punto de emisión, bodega, cliente o cuenta contable inválidos, o código de caja ya existente en la sucursal.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCashRegisterCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Actualiza nombre, notas, punto de emisión, bodega, cliente y cuenta contable de una caja.</summary>
    /// <remarks>
    /// Código y sucursal son inmutables. Una caja con historial operativo (aperturas, movimientos o
    /// cierres) no puede reasignar su punto de emisión: debe desactivarse y crearse una nueva.
    /// </remarks>
    /// <param name="id">Id de la caja; debe coincidir con <c>id</c> del cuerpo.</param>
    /// <param name="command">Nuevos datos de la caja.</param>
    /// <response code="200">Caja actualizada.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La caja no existe.</response>
    /// <response code="422">Datos inválidos o cambio de punto de emisión en una caja con historial.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCashRegisterCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>Deshabilita una caja (baja lógica; no se elimina).</summary>
    /// <remarks>Se rechaza si la caja tiene una sesión de caja abierta: hay que cerrarla antes.</remarks>
    /// <param name="id">Id de la caja.</param>
    /// <response code="200">Caja deshabilitada; <c>data</c> es <c>true</c>.</response>
    /// <response code="404">La caja no existe.</response>
    /// <response code="422">La caja tiene una sesión abierta.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableCashRegisterCommand(id), ct));

    /// <summary>Habilita una caja deshabilitada.</summary>
    /// <param name="id">Id de la caja.</param>
    /// <response code="200">Caja habilitada; <c>data</c> es <c>true</c>.</response>
    /// <response code="404">La caja no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CajaPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableCashRegisterCommand(id), ct));
}
