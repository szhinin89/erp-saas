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

    [HttpPut("{id:guid}/unit-conversions")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
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
