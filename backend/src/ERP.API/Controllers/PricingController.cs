using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Pricing.UseCases.CustomerPriceListContext;
using ERP.Application.Modules.Pricing.UseCases.PriceListCustomers;
using ERP.Application.Modules.Pricing.UseCases.PriceListItems;
using ERP.Application.Modules.Pricing.UseCases.PriceLists;
using ERP.Application.Modules.Pricing.UseCases.PricingRules;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Listas de Precios", $"perm:{PricingPermissions.View}", "💰", "/pricing", null, 50)]
[ApiController]
[Route("api/v1/pricing")]
[Authorize]
[Produces("application/json")]
public sealed class PricingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PricingController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // PRICE LISTS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("price-lists")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPriceLists(
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPriceListsQuery(isActive, search), ct),
            "OK"
        );

    [HttpGet("price-lists/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPriceListById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPriceListByIdQuery(id), ct));

    [HttpPost("price-lists")]
    [Authorize(Policy = $"perm:{PricingPermissions.Create}")]
    public async Task<IActionResult> CreatePriceList(
        [FromBody] CreatePriceListCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    [HttpPut("price-lists/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> UpdatePriceList(
        Guid id,
        [FromBody] UpdatePriceListCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    [HttpPatch("price-lists/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> EnablePriceList(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnablePriceListCommand(id), ct));

    [HttpPatch("price-lists/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> DisablePriceList(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisablePriceListCommand(id), ct));

    /// <summary>Único punto de escritura para "lista de precios predeterminada" — consumido por Configuración → Ventas (settings/company).</summary>
    [HttpPatch("price-lists/{id:guid}/set-default")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> SetDefaultPriceList(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new SetDefaultPriceListCommand(id), ct));

    /// <summary>Ítems pertenecientes a esta lista (identidad + precio base) — sin reglas ni excepciones; combinar con GetPricingRules en el consumidor.</summary>
    [HttpGet("price-lists/{id:guid}/assigned-items")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetAssignedItems(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetItemsAssignedToPriceListQuery(id), ct),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // PRICE LIST CUSTOMERS (PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B) — todavía NO
    // consumido por Sales, solo administración desde /products/pricing.
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SALES-PRICING-UX-TRACEABILITY-07C: contexto read-only de listas de un cliente (lista propia
    /// asignada vs. default de la empresa) para la cabecera de Ventas. Autoriza con el permiso de
    /// lectura de Ventas porque su consumidor es el cajero, no el administrador de precios.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/price-list-context")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetCustomerPriceListContext(Guid customerId, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCustomerPriceListContextQuery(customerId), ct),
            "OK"
        );

    /// <summary>Clientes actualmente asignados (activos) a esta lista.</summary>
    [HttpGet("price-lists/{id:guid}/customers")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPriceListCustomers(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPriceListCustomersQuery(id), ct),
            "OK"
        );

    /// <summary>
    /// Asigna un cliente a esta lista. Si el cliente ya tiene otra lista activa, devuelve
    /// Status=Conflict sin escribir nada (nunca un error técnico) — el llamador debe reenviar
    /// con ConfirmSwitch=true tras confirmación explícita del usuario para completar el cambio.
    /// </summary>
    [HttpPost("price-lists/{id:guid}/customers")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> AssignCustomerToPriceList(
        Guid id,
        [FromBody] AssignPriceListCustomerRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new AssignCustomerToPriceListCommand(id, request.CustomerId, request.ConfirmSwitch),
                ct
            ),
            "OK"
        );

    /// <summary>Quita (desactiva) la asignación de un cliente a esta lista — nunca borrado físico.</summary>
    [HttpDelete("price-lists/{id:guid}/customers/{customerId:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> RemovePriceListCustomer(
        Guid id,
        Guid customerId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisablePriceListCustomerCommand(id, customerId), ct)
        );

    // ══════════════════════════════════════════════════════════════════════
    // PRICING RULES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("pricing-rules")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPricingRules(
        [FromQuery] Guid? priceListId = null,
        [FromQuery] Guid? itemId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPricingRulesQuery(priceListId, itemId), ct),
            "OK"
        );

    [HttpGet("pricing-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPricingRuleById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPricingRuleByIdQuery(id), ct));

    [HttpPost("pricing-rules")]
    [Authorize(Policy = $"perm:{PricingPermissions.Create}")]
    public async Task<IActionResult> SetPricingRule(
        [FromBody] SetPricingRuleCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Reactivación explícita de una excepción deshabilitada — nunca se dispara automáticamente desde SetPricingRule.</summary>
    [HttpPost("pricing-rules/enable")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> EnablePricingRule(
        [FromBody] EnablePricingRuleCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    [HttpDelete("pricing-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.Delete}")]
    public async Task<IActionResult> RemovePricingRule(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new RemovePricingRuleCommand(id), ct));
}

public sealed record AssignPriceListCustomerRequest(Guid CustomerId, bool ConfirmSwitch = false);
