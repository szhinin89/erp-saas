using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
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
public sealed class ItemPackagingLevelsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemPackagingLevelsController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // PACKAGING LEVELS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reemplaza las presentaciones (empaques) de un ítem.
    /// </summary>
    /// <remarks>
    /// El conjunto enviado sustituye al actual. Un producto exige exactamente una presentación base con cantidad
    /// base 1; un servicio admite como máximo una. No se repite la combinación de unidad de medida y cantidad base.
    /// No se puede quitar una presentación usada por códigos de proveedor o por documentos confirmados, ni cambiar
    /// la cantidad base de una usada en documentos confirmados.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Lista de presentaciones que quedarán asociadas al ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Presentaciones del ítem actualizadas.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/packaging-levels")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplacePackagingLevels(
        Guid id,
        [FromBody] ReplacePackagingRequest request,
        CancellationToken cancellationToken
    )
    {
        var cmd = new ReplaceItemPackagingLevelsCommand(
            id,
            request
                .Levels.Select(l => new PackagingLevelInput(
                    l.Id,
                    l.Name,
                    l.Level,
                    l.BaseQuantity,
                    l.UomCode,
                    l.Barcode,
                    l.Weight,
                    l.IsBaseUnit,
                    l.IsPurchaseDefault,
                    l.IsSaleDefault
                ))
                .ToList()
        );
        return this.ToOkOrBadRequest(await _mediator.Send(cmd, cancellationToken));
    }
}

// ── Request body types ─────────────────────────────────────────────────────

public record ReplacePackagingRequest(IReadOnlyList<PackagingApiInput> Levels);

public record PackagingApiInput(
    Guid? Id,
    string Name,
    int Level,
    decimal BaseQuantity,
    string UomCode,
    string? Barcode = null,
    decimal? Weight = null,
    bool IsBaseUnit = false,
    bool IsPurchaseDefault = false,
    bool IsSaleDefault = false
);
