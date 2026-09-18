using ERP.API.Attributes;
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
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPaymentMethodsQuery(onlyActive), ct),
            "OK"
        );

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPaymentMethodByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = $"perm:{SalesPermissions.Create}")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentMethodCommand cmd,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(cmd, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
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
    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{AccountingPermissions.DestinationsSalesCollectionsUpdate}")]
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
    [HttpPut("{id:guid}/account")]
    [Authorize(Policy = $"perm:{AccountingPermissions.DestinationsSalesCollectionsUpdate}")]
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
