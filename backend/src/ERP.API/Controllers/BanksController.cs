using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.MasterData.UseCases.Banks;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// BANK-CATALOG-01: catálogo maestro de bancos — Configuración > Catálogos > Bancos. CRUD básico,
// sin cuenta contable ni relación con PaymentMethodAccount (eso pertenece a un módulo
// bancario futuro, fuera de este ticket).
[AppFeature(
    "Instituciones bancarias",
    $"perm:{SettingsPermissions.BanksView}",
    "🏦",
    "/settings/catalogs/banks",
    null,
    66
)]
[ApiController]
[Route("api/v1/settings/catalogs/banks")]
[Authorize]
[Produces("application/json")]
public sealed class BanksController : ControllerBase
{
    private readonly IMediator _mediator;

    public BanksController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista los bancos del catálogo del tenant, ordenados por nombre.</summary>
    /// <remarks>
    /// A diferencia del resto de operaciones del catálogo, responde con el arreglo de bancos
    /// directamente, sin el envelope <c>ApiResponse</c>.
    /// </remarks>
    /// <param name="onlyActive">Si es <c>true</c> devuelve solo los bancos activos; por defecto <c>false</c> (incluye inactivos).</param>
    /// <param name="search">Texto opcional: filtra por coincidencia parcial, sin distinguir mayúsculas, en código, nombre o nombre corto.</param>
    /// <response code="200">Arreglo JSON de bancos (sin envelope).</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksView}")]
    [ProducesResponseType(typeof(IReadOnlyList<BankDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool onlyActive = false,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    ) => Ok(await _mediator.Send(new ListBanksQuery(onlyActive, search), ct));

    /// <summary>Obtiene un banco del catálogo por su id.</summary>
    /// <param name="id">Id del banco.</param>
    /// <response code="200">Banco encontrado.</response>
    /// <response code="404">El banco no existe en el tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksView}")]
    [ProducesResponseType(typeof(ApiResponse<BankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetBankByIdQuery(id), ct));

    /// <summary>Crea un banco en el catálogo del tenant.</summary>
    /// <remarks>
    /// El código es único por tenant y país; se compara en mayúsculas y sin espacios. El país
    /// (<c>countryCode</c>, 2 letras) es <c>EC</c> si no se envía. Longitudes máximas: código 30,
    /// nombre 150, nombre corto 50.
    /// </remarks>
    /// <param name="cmd">Código, nombre, nombre corto opcional y país.</param>
    /// <response code="201">Banco creado.</response>
    /// <response code="409">Ya existe un banco con ese código para el país.</response>
    /// <response code="422">Datos inválidos (campos obligatorios o longitudes excedidas).</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksCreate}")]
    [ProducesResponseType(typeof(ApiResponse<BankDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct), "bank");

    /// <summary>Actualiza el nombre y el nombre corto de un banco.</summary>
    /// <remarks>El código y el país no se pueden modificar.</remarks>
    /// <param name="id">Id del banco; debe coincidir con <c>id</c> del cuerpo.</param>
    /// <param name="cmd">Id, nombre y nombre corto opcional.</param>
    /// <response code="200">Banco actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">El banco no existe en el tenant.</response>
    /// <response code="422">Datos inválidos (nombre obligatorio o longitudes excedidas).</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<BankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBankCommand cmd,
        CancellationToken ct
    )
    {
        if (id != cmd.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
    }

    /// <summary>Reactiva un banco deshabilitado.</summary>
    /// <remarks>Los bancos no se eliminan: se habilitan o deshabilitan. Habilitar uno ya activo se rechaza con 422.</remarks>
    /// <param name="id">Id del banco.</param>
    /// <response code="200">Banco habilitado (<c>data</c> es <c>true</c>).</response>
    /// <response code="404">El banco no existe en el tenant.</response>
    /// <response code="422">El banco ya estaba activo.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksManage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableBankCommand(id), ct), "Activado.");

    /// <summary>Deshabilita un banco (baja lógica).</summary>
    /// <remarks>Los bancos no se eliminan. Deshabilitar uno ya deshabilitado se rechaza con 422.</remarks>
    /// <param name="id">Id del banco.</param>
    /// <response code="200">Banco deshabilitado (<c>data</c> es <c>true</c>).</response>
    /// <response code="404">El banco no existe en el tenant.</response>
    /// <response code="422">El banco ya estaba deshabilitado.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{SettingsPermissions.BanksManage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableBankCommand(id), ct), "Desactivado.");
}
