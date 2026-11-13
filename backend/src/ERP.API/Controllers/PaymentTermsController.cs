using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.MasterData.UseCases.PaymentTerms;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Condiciones de Pago",
    $"perm:{MasterDataPermissions.PaymentTermsView}",
    "💳",
    "/master/payment-terms",
    null,
    58
)]
[ApiController]
[Route("api/v1/master/payment-terms")]
[Authorize]
[Produces("application/json")]
public sealed class PaymentTermsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentTermsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista las condiciones de pago de la empresa.
    /// </summary>
    /// <remarks>
    /// Devuelve directamente el arreglo de condiciones, sin envelope. Se puede filtrar por texto.
    /// </remarks>
    /// <param name="search">Texto que se busca en el código o el nombre; omitido, lista todas.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Arreglo de condiciones de pago.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsView}")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentTermDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        CancellationToken ct = default
    ) => Ok(await _mediator.Send(new ListPaymentTermsQuery(search), ct));

    /// <summary>
    /// Obtiene una condición de pago por id.
    /// </summary>
    /// <param name="id">Id de la condición de pago.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Condición de pago.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsView}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentTermDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPaymentTermByIdQuery(id), ct));

    /// <summary>
    /// Crea una condición de pago.
    /// </summary>
    /// <remarks>
    /// El código debe ser único en la empresa. Responde 201 con la condición creada.
    /// </remarks>
    /// <param name="cmd">Código, nombre, número de cuotas y días entre cuotas.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Condición de pago creada.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentTermDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentTermCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct), "payment-term");

    /// <summary>
    /// Actualiza una condición de pago.
    /// </summary>
    /// <remarks>
    /// El código no se modifica. El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400
    /// con un texto plano, sin envelope.
    /// </remarks>
    /// <param name="id">Id de la condición de pago.</param>
    /// <param name="cmd">Id, nombre, número de cuotas y días entre cuotas.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Condición de pago actualizada.</response>
    /// <response code="400">La operación incumple una regla de negocio (envelope). Si el id de la ruta no coincide con el del cuerpo responde 400 con texto plano, sin envelope.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentTermDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePaymentTermCommand cmd,
        CancellationToken ct
    )
    {
        if (id != cmd.Id)
            return BadRequest("ID mismatch.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct), "Actualizado.");
    }

    /// <summary>
    /// Activa una condición de pago.
    /// </summary>
    /// <param name="id">Id de la condición de pago.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Condición de pago activada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new EnablePaymentTermCommand(id), ct),
            "Activado."
        );

    /// <summary>
    /// Desactiva una condición de pago.
    /// </summary>
    /// <remarks>
    /// Baja lógica: la condición deja de ofrecerse pero no se elimina.
    /// </remarks>
    /// <param name="id">Id de la condición de pago.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Condición de pago desactivada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{MasterDataPermissions.PaymentTermsManage}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisablePaymentTermCommand(id), ct),
            "Desactivado."
        );
}
