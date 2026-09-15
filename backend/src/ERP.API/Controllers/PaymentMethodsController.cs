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

    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = true,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPaymentMethodsQuery(onlyActive), ct),
            "OK"
        );

    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
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

    [HttpPost("{id:guid}/toggle")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new TogglePaymentMethodCommand(id), ct));

    /// <summary>
    /// SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — configura, para la Company activa, la cuenta
    /// contable (Caja/Bancos) que debe recibir el débito de "dinero real cobrado" cuando este
    /// método de pago se usa en una venta. Sin esta configuración, toda venta con este método
    /// (salvo Efectivo, ya vinculado por defecto a Caja general) es rechazada al autorizar.
    /// </summary>
    [HttpPut("{id:guid}/account")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
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
