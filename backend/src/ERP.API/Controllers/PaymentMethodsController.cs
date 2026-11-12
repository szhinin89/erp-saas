using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Métodos de Pago",
    $"perm:{SalesPermissions.View}",
    "💳",
    "/sales/payment-methods",
    null,
    26
)]
[ApiController]
[Route("api/v1/payment-methods")]
[Authorize]
[Produces("application/json")]
public sealed class PaymentMethodsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentMethodsController(IMediator mediator) => _mediator = mediator;

    // DESTINOS-CONTABLES-COBROS-VENTAS-01 — lookup compartido: lo consume tanto Ventas/POS
    // (selector de forma de pago al cobrar) como Contabilidad (pantalla "Cobros de ventas").
    // Ninguno de los dos permisos de dominio (sales.view / accounting.destinations
    // .sales_collections.view) puede ser el único requisito sin romper al otro consumidor, y no
    // existe primitiva de autorización "cualquiera de estos permisos" en este backend — mismo
    // patrón ya usado por los lookups SRI de solo lectura en CatalogController (sri-vat-rates,
    // sri-payment-methods, etc.): [Authorize] simple (sesión + tenant autenticados, sin permiso
    // de negocio específico). El catálogo en sí (código/nombre/¿requiere referencia?/activo) no
    // es dato sensible; las acciones que sí lo son (activar/desactivar, asignar cuenta contable)
    // siguen exigiendo su permiso específico más abajo.
    /// <summary>Lista los métodos de pago, con la fuente de su cuenta contable.</summary>
    /// <param name="onlyActive">Si es <c>true</c> (valor por defecto) solo devuelve los activos.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Métodos de pago (lista vacía si no hay).</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<PaymentMethodDto>>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPaymentMethodsQuery(onlyActive), ct),
            "OK"
        );

    /// <summary>Obtiene un método de pago por su identificador.</summary>
    /// <param name="id">Identificador del método de pago.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Método de pago encontrado.</response>
    /// <response code="404">El método de pago no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPaymentMethodByIdQuery(id), ct));

    /// <summary>Crea un método de pago (activo) del tenant.</summary>
    /// <remarks>
    /// El código debe ser único en el tenant. Si se informa <c>sriPaymentMethodCode</c> debe existir y estar activo
    /// en el catálogo SRI de formas de pago.
    /// </remarks>
    /// <param name="cmd">Código, nombre y atributos del método de pago.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Método de pago creado.</response>
    /// <response code="422">Datos inválidos, código duplicado o código SRI inexistente o inactivo.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SalesPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentMethodCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    /// <summary>Actualiza nombre y atributos de un método de pago (el código no cambia).</summary>
    /// <param name="id">Identificador del método de pago; debe coincidir con <c>id</c> del cuerpo.</param>
    /// <param name="cmd">Nuevos valores del método de pago.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Método de pago actualizado.</response>
    /// <response code="400">El identificador de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">El método de pago no existe.</response>
    /// <response code="422">Datos inválidos o código SRI inexistente o inactivo.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePaymentMethodCommand cmd,
        CancellationToken ct
    )
    {
        if (id != cmd.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, ct));
    }

    // DESTINOS-CONTABLES-COBROS-VENTAS-01 — Toggle solo lo llama la pantalla "Cobros de ventas"
    // (única entrada de menú de este controller, bajo Contabilidad); ya no hay pantalla de
    // catálogo bajo Ventas que lo use, así que exige el permiso contable dedicado en vez de
    // sales.update.
    /// <summary>Alterna el estado activo/inactivo de un método de pago.</summary>
    /// <remarks>
    /// No es idempotente: cada llamada invierte el estado actual (activo ↔ inactivo). Baja lógica; el método
    /// nunca se elimina.
    /// </remarks>
    /// <param name="id">Identificador del método de pago.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Método de pago con su nuevo estado.</response>
    /// <response code="404">El método de pago no existe.</response>
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{AccountingPermissions.DestinationsSalesCollectionsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new TogglePaymentMethodCommand(id), ct));

    /// <summary>
    /// SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — configura, para la Company activa, la cuenta
    /// contable que debe recibir el débito de "dinero real cobrado" cuando este método de pago se
    /// usa en una venta. Solo aplica a métodos Tarjeta/Cheque (rechazado en Application si el
    /// método es Efectivo/Transferencia/Crédito — esos resuelven su cuenta desde
    /// CashRegister/CompanyBankAccount/reglas de CxC, nunca desde aquí). Sin esta configuración,
    /// toda venta con Tarjeta/Cheque es rechazada al autorizar.
    /// DESTINOS-CONTABLES-COBROS-VENTAS-01 — exige el permiso contable dedicado (no
    /// sales.update): es una configuración contable, no una acción de Ventas.
    /// </summary>
    /// <remarks>
    /// Crea o reemplaza la asignación de la empresa activa (idempotente para el mismo valor). La cuenta debe
    /// existir en la empresa, estar activa y admitir movimientos. Un identificador de método inexistente se
    /// rechaza en la validación previa con 422 (no 404).
    /// </remarks>
    /// <param name="id">Identificador del método de pago (Tarjeta o Cheque).</param>
    /// <param name="body">Cuenta contable que recibirá el débito del cobro.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Método de pago con la cuenta contable configurada.</response>
    /// <response code="422">Método inexistente, no es Tarjeta/Cheque, o cuenta contable inexistente, inactiva o que no admite movimientos.</response>
    [HttpPut("{id:guid}/account")]
    [Authorize(Policy = $"perm:{AccountingPermissions.DestinationsSalesCollectionsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetAccount(
        Guid id,
        [FromBody] SetPaymentMethodAccountRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SetPaymentMethodAccountCommand(id, body.AccountingAccountId),
                ct
            )
        );
}

public sealed record SetPaymentMethodAccountRequest(Guid AccountingAccountId);
