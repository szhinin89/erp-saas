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

    /// <summary>
    /// Reemplaza los ítems sustitutos de un ítem.
    /// </summary>
    /// <remarks>
    /// El conjunto enviado sustituye al actual. Los sustitutos deben ser ítems de la misma empresa y un ítem no
    /// puede ser sustituto de sí mismo. Cada sustituto lleva prioridad y nota.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Lista de sustitutos con prioridad y nota.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Sustitutos del ítem actualizados.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/substitutes")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
