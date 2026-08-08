using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Items.UseCases.Profitability;
using ERP.Application.Modules.Pricing.UseCases.ItemPricingSimulation;
using ERP.Application.Modules.Pricing.UseCases.PriceListItems;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// Split out of ItemsController (B-controller max-lines) — mismo Route base, mismos
// endpoints, mismos permisos. Ver ItemsController.cs para el resto del recurso Items.
[ApiController]
[Route("api/v1/items")]
[Authorize]
[Produces("application/json")]
public sealed class ItemPricingController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemPricingController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // PROFITABILITY & PRICE SIMULATION
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("{id:guid}/profitability")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    public async Task<IActionResult> GetProfitability(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetItemProfitabilityQuery(id), ct));

    [HttpPost("{id:guid}/simulate-price")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    public async Task<IActionResult> SimulatePrice(
        Guid id,
        [FromBody] SimulatePriceRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SimulateItemPricingQuery(id, request.NewPvp), ct)
        );

    /// <summary>
    /// Simulación de precio contra todas las listas de precio activas y vigentes del ítem.
    /// Los overrides son "qué pasaría si" (formulario aún no guardado) — sin ellos, usa el
    /// valor ya persistido. Nada de lo calculado aquí se guarda.
    /// </summary>
    [HttpPost("{id:guid}/pricing-simulation")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemPricingSimulationRowDto>>), 200)]
    public async Task<IActionResult> GetPricingSimulation(
        Guid id,
        [FromBody] PricingSimulationRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetItemPricingSimulationQuery(
                    id,
                    request.BaseSalePrice,
                    request.MaxDiscountPercent
                ),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Misma simulación que la de arriba, pero para un ítem que todavía no existe (formulario
    /// de creación) — sin ItemId no hay reglas por-ítem ni asignaciones posibles, solo las
    /// reglas generales de cada PriceList. BaseSalePrice es obligatorio aquí (no hay ítem
    /// persistido del cual heredarlo).
    /// </summary>
    [HttpPost("pricing-simulation-preview")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemPricingSimulationRowDto>>), 200)]
    public async Task<IActionResult> GetPricingSimulationPreview(
        [FromBody] PricingSimulationRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetItemPricingSimulationQuery(
                    null,
                    request.BaseSalePrice,
                    request.MaxDiscountPercent
                ),
                ct
            ),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // PRICE LISTS (asignación administrativa — no crea reglas ni precios)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("{id:guid}/price-lists")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), 200)]
    public async Task<IActionResult> GetPriceLists(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetItemPriceListsQuery(id), cancellationToken),
            "OK"
        );

    [HttpPut("{id:guid}/price-lists")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), 200)]
    public async Task<IActionResult> SetPriceLists(
        Guid id,
        [FromBody] SetItemPriceListsRequest request,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SetItemPriceListsCommand(id, request.PriceListIds),
                cancellationToken
            )
        );
}

// ── Request body types ─────────────────────────────────────────────────────

public record SimulatePriceRequest(decimal NewPvp);

public record PricingSimulationRequest(
    decimal? BaseSalePrice = null,
    decimal? MaxDiscountPercent = null
);

public record SetItemPriceListsRequest(IReadOnlyList<Guid> PriceListIds);
