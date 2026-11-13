using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Items.UseCases.Profitability;
using ERP.Application.Modules.Pricing.UseCases.ItemPricingSimulation;
using ERP.Application.Modules.Pricing.UseCases.PriceListItems;
using ERP.Application.Modules.Pricing.UseCases.PricingRules;
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

    /// <summary>
    /// Calcula la rentabilidad de un ítem.
    /// </summary>
    /// <remarks>
    /// Consulta de solo lectura con el costo y el precio de venta vigentes del ítem.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Rentabilidad del ítem.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}/profitability")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemProfitabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfitability(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetItemProfitabilityQuery(id), ct));

    /// <summary>
    /// Simula un nuevo precio de venta para un ítem.
    /// </summary>
    /// <remarks>
    /// Consulta de solo lectura: calcula el efecto del precio indicado y no guarda nada.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Nuevo precio de venta a simular.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la simulación.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/simulate-price")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<PriceSimulationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SimulatePrice(
        Guid id,
        [FromBody] SimulatePriceRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new SimulateItemPricingQuery(id, request.NewPvp), ct)
        );

    /// <summary>
    /// Simula el precio de un ítem en todas sus listas de precio vigentes.
    /// </summary>
    /// <remarks>
    /// Simulación contra todas las listas de precio activas y vigentes del ítem. Los valores enviados como
    /// <c>baseSalePrice</c>, <c>maxDiscountPercent</c> y <c>exceptionPreview</c> son del tipo «qué pasaría si»
    /// (formulario no guardado); sin ellos usa el valor persistido. Nada de lo calculado se guarda.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Valores opcionales a simular en lugar de los persistidos.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Simulación por lista de precio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/pricing-simulation")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemPricingSimulationRowDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
                    request.MaxDiscountPercent,
                    request.ExceptionPreview
                ),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Simula el precio de un ítem que aún no existe.
    /// </summary>
    /// <remarks>
    /// Misma simulación que la del ítem, para el formulario de creación: sin ítem no hay reglas por ítem ni
    /// asignaciones, solo las reglas generales de cada lista de precio. <c>baseSalePrice</c> es obligatorio porque
    /// no hay un ítem persistido del que heredarlo. Nada se guarda.
    /// </remarks>
    /// <param name="request">Precio base y demás valores a simular.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Simulación por lista de precio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("pricing-simulation-preview")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemPricingSimulationRowDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPricingSimulationPreview(
        [FromBody] PricingSimulationRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetItemPricingSimulationQuery(
                    null,
                    request.BaseSalePrice,
                    request.MaxDiscountPercent,
                    request.ExceptionPreview
                ),
                ct
            ),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // PRICE LISTS (asignación administrativa — no crea reglas ni precios)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las listas de precio asignadas a un ítem.
    /// </summary>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Listas de precio del ítem.</response>
    [HttpGet("{id:guid}/price-lists")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), 200)]
    public async Task<IActionResult> GetPriceLists(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetItemPriceListsQuery(id), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Asigna las listas de precio de un ítem.
    /// </summary>
    /// <remarks>
    /// El conjunto de identificadores enviado define las listas de precio del ítem.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Ids de las listas de precio a asignar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Listas de precio del ítem actualizadas.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/price-lists")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Guid>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
    decimal? MaxDiscountPercent = null,
    SetPricingRuleCommand? ExceptionPreview = null
);

public record SetItemPriceListsRequest(IReadOnlyList<Guid> PriceListIds);
