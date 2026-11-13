using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.ItemUnitConversions;
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
public sealed class ItemUnitConversionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemUnitConversionsController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // UNIT CONVERSIONS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reemplaza las conversiones de unidad de medida de un ítem.
    /// </summary>
    /// <remarks>
    /// El conjunto enviado sustituye al actual. Cada conversión define unidad de origen, unidad de destino y un
    /// factor mayor que cero.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Lista de conversiones de unidad.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Conversiones del ítem actualizadas.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/unit-conversions")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceUnitConversions(
        Guid id,
        [FromBody] ReplaceConversionsRequest request,
        CancellationToken cancellationToken
    )
    {
        var cmd = new ReplaceItemUnitConversionsCommand(
            id,
            request
                .Conversions.Select(c => new UnitConversionInput(
                    c.FromUomCode,
                    c.ToUomCode,
                    c.Factor
                ))
                .ToList()
        );
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, cancellationToken));
    }
}

// ── Request body types ─────────────────────────────────────────────────────

public record ReplaceConversionsRequest(IReadOnlyList<ConversionApiInput> Conversions);

public record ConversionApiInput(string FromUomCode, string ToUomCode, decimal Factor);
