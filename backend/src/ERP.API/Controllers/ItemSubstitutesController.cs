using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.ItemSubstitutes;
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
public sealed class ItemSubstitutesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemSubstitutesController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // SUBSTITUTES
    // ══════════════════════════════════════════════════════════════════════

    [HttpPut("{id:guid}/substitutes")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    public async Task<IActionResult> ReplaceSubstitutes(
        Guid id,
        [FromBody] ReplaceSubstitutesRequest request,
        CancellationToken cancellationToken
    )
    {
        var cmd = new ReplaceItemSubstitutesCommand(
            id,
            request
                .Substitutes.Select(s => new SubstituteInput(
                    s.SubstituteItemId,
                    s.Priority,
                    s.Note
                ))
                .ToList()
        );
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, cancellationToken));
    }
}

// ── Request body types ─────────────────────────────────────────────────────

public record ReplaceSubstitutesRequest(IReadOnlyList<SubstituteApiInput> Substitutes);

public record SubstituteApiInput(Guid SubstituteItemId, int Priority = 1, string? Note = null);
